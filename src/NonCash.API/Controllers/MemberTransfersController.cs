using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/member/transfers")]
[Authorize]
public class MemberTransfersController : ControllerBase
{
    private readonly IVoucherTransferService _transferService;

    public MemberTransfersController(IVoucherTransferService transferService)
    {
        _transferService = transferService;
    }

    private Guid GetCurrentMemberId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
    }

    // AC1: List pending inbound transfers
    [HttpGet("inbox")]
    public async Task<ActionResult<IEnumerable<TransferInboxDto>>> Inbox(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var memberId = GetCurrentMemberId();
        if (memberId == Guid.Empty)
            return Unauthorized(new { error = "Unauthorized", message = "Member identity is required." });

        VoucherTransferStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<VoucherTransferStatus>(status, true, out var parsed))
            statusFilter = parsed;

        var items = await _transferService.GetInboxAsync(memberId, statusFilter, page, pageSize, cancellationToken);
        return Ok(items.Select(i => new TransferInboxDto(
            i.TransferId,
            i.VoucherId,
            i.SerialNo,
            i.BrandName,
            i.FaceValue,
            i.ValueType,
            i.ExpiryDate,
            i.SenderId,
            i.SenderDisplayName,
            i.Note,
            i.Status.ToString(),
            i.InitiatedAt,
            i.ExpiresAt,
            i.RespondedAt,
            i.MessageCount,
            i.LastMessageAt,
            i.UnreadCount)));
    }

    // AC3: Outgoing transfer history
    [HttpGet("outbox")]
    public async Task<ActionResult<IEnumerable<TransferOutboxDto>>> Outbox(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var memberId = GetCurrentMemberId();
        if (memberId == Guid.Empty)
            return Unauthorized(new { error = "Unauthorized", message = "Member identity is required." });

        VoucherTransferStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<VoucherTransferStatus>(status, true, out var parsed))
            statusFilter = parsed;

        var items = await _transferService.GetOutboxAsync(memberId, statusFilter, page, pageSize, cancellationToken);
        return Ok(items.Select(i => new TransferOutboxDto(
            i.TransferId,
            i.VoucherId,
            i.SerialNo,
            i.BrandName,
            i.FaceValue,
            i.ValueType,
            i.ExpiryDate,
            i.RecipientId,
            i.RecipientDisplayName,
            i.Note,
            i.Status.ToString(),
            i.InitiatedAt,
            i.ExpiresAt,
            i.RespondedAt,
            i.RecipientNote,
            i.RejectReason,
            i.MessageCount,
            i.LastMessageAt,
            i.UnreadCount)));
    }

    // AC2: Accept a gift, with the recipient's thank-you note (CR-2026-09-10-30 row 4)
    [HttpPost("{transferId:guid}/accept")]
    public async Task<ActionResult<TransferActionDto>> Accept(
        Guid transferId,
        [FromBody] AcceptGiftRequest? request,
        CancellationToken cancellationToken)
    {
        var memberId = GetCurrentMemberId();
        if (memberId == Guid.Empty)
            return Unauthorized(new { error = "Unauthorized", message = "Member identity is required." });

        var result = await _transferService.AcceptAsync(transferId, memberId, request?.Note, cancellationToken);
        return MapActionResult(result);
    }

    // AC3: Reject transfer
    [HttpPost("{transferId:guid}/reject")]
    public async Task<ActionResult<TransferActionDto>> Reject(
        Guid transferId,
        [FromBody] RejectTransferRequest? request,
        CancellationToken cancellationToken)
    {
        var memberId = GetCurrentMemberId();
        if (memberId == Guid.Empty)
            return Unauthorized(new { error = "Unauthorized", message = "Member identity is required." });

        var result = await _transferService.RejectAsync(transferId, memberId, request?.Reason, cancellationToken);
        return MapActionResult(result);
    }

    // Story 5-3 AC1: Cancel pending transfer
    [HttpPost("{transferId:guid}/cancel")]
    public async Task<ActionResult<TransferActionDto>> Cancel(Guid transferId, CancellationToken cancellationToken)
    {
        var memberId = GetCurrentMemberId();
        if (memberId == Guid.Empty)
            return Unauthorized(new { error = "Unauthorized", message = "Member identity is required." });

        var result = await _transferService.CancelAsync(transferId, memberId, cancellationToken);
        return MapActionResult(result);
    }

    /// <summary>CR-2026-09-10-31: the conversation kept with a gift. Opening it marks the messages
    /// addressed to this member as read.</summary>
    [HttpGet("{transferId:guid}/messages")]
    public async Task<ActionResult<GiftThreadDto>> GetMessages(Guid transferId, CancellationToken cancellationToken)
    {
        var memberId = GetCurrentMemberId();
        if (memberId == Guid.Empty)
            return Unauthorized(new { error = "Unauthorized", message = "Member identity is required." });

        var thread = await _transferService.GetThreadAsync(transferId, memberId, cancellationToken);
        if (!thread.Success)
            return MapThreadFailure(thread);

        return Ok(new GiftThreadDto(
            transferId,
            thread.Status ?? string.Empty,
            thread.CanReply,
            thread.ClosedReason,
            thread.Messages!.Select(ToDto)));
    }

    /// <summary>CR-2026-09-10-31: a member adds a message to a gift's conversation.</summary>
    [HttpPost("{transferId:guid}/messages")]
    public async Task<ActionResult<GiftMessageDto>> PostMessage(
        Guid transferId,
        [FromBody] PostGiftMessageRequest? request,
        CancellationToken cancellationToken)
    {
        var memberId = GetCurrentMemberId();
        if (memberId == Guid.Empty)
            return Unauthorized(new { error = "Unauthorized", message = "Member identity is required." });

        var result = await _transferService.PostMessageAsync(transferId, memberId, request?.Body, cancellationToken);
        if (!result.Success)
        {
            return result.ErrorCode switch
            {
                "TransferNotFound" => NotFound(new { error = result.ErrorCode, message = result.ErrorMessage }),
                "Forbidden" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.ErrorCode, message = result.ErrorMessage }),
                "ConversationClosed" => Conflict(new { error = result.ErrorCode, message = result.ErrorMessage }),
                "MessageLimitReached" or "RateLimited" =>
                    StatusCode(StatusCodes.Status429TooManyRequests, new { error = result.ErrorCode, message = result.ErrorMessage }),
                _ => BadRequest(new { error = result.ErrorCode ?? "Validation", message = result.ErrorMessage })
            };
        }

        return StatusCode(StatusCodes.Status201Created, ToDto(result.Message!));
    }

    private ActionResult<GiftThreadDto> MapThreadFailure(GiftThreadResult thread) => thread.ErrorCode switch
    {
        "TransferNotFound" => NotFound(new { error = thread.ErrorCode, message = thread.ErrorMessage }),
        _ => StatusCode(StatusCodes.Status403Forbidden, new { error = thread.ErrorCode, message = thread.ErrorMessage })
    };

    private static GiftMessageDto ToDto(GiftMessageItem item) => new(
        item.MessageId,
        item.TransferId,
        item.AuthorMemberId,
        item.AuthorDisplayName,
        item.Direction.ToString(),
        item.Kind.ToString(),
        item.Body,
        item.SentAt,
        item.IsRead);

    private ActionResult<TransferActionDto> MapActionResult(TransferActionResult result)
    {
        if (!result.Success)
        {
            return result.ErrorCode switch
            {
                "TransferNotFound" => NotFound(new { error = result.ErrorCode, message = result.ErrorMessage }),
                "Forbidden" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.ErrorCode, message = result.ErrorMessage }),
                "AlreadyResolved" => Conflict(new TransferActionDto(result.Status ?? "Resolved", result.VoucherId)),
                _ => BadRequest(new { error = result.ErrorCode ?? "Validation", message = result.ErrorMessage })
            };
        }

        return Ok(new TransferActionDto(result.Status ?? string.Empty, result.VoucherId));
    }
}

public record TransferInboxDto(
    Guid TransferId,
    Guid VoucherId,
    string SerialNo,
    string BrandName,
    decimal FaceValue,
    string? ValueType,
    DateTime ExpiryDate,
    Guid SenderId,
    string SenderDisplayName,
    string? Note,
    string Status,
    DateTime InitiatedAt,
    DateTime ExpiresAt,
    DateTime? RespondedAt = null,
    int MessageCount = 0,
    DateTime? LastMessageAt = null,
    int UnreadCount = 0);

public record TransferOutboxDto(
    Guid TransferId,
    Guid VoucherId,
    string SerialNo,
    string BrandName,
    decimal FaceValue,
    string? ValueType,
    DateTime ExpiryDate,
    Guid RecipientId,
    string RecipientDisplayName,
    string? Note,
    string Status,
    DateTime InitiatedAt,
    DateTime ExpiresAt,
    DateTime? RespondedAt,
    string? RecipientNote = null,
    string? RejectReason = null,
    int MessageCount = 0,
    DateTime? LastMessageAt = null,
    int UnreadCount = 0);

public record TransferActionDto(string Status, Guid? VoucherId);

/// <summary>CR-2026-09-10-30 row 4: the thank-you note the recipient sends back with the gift.</summary>
public record AcceptGiftRequest(string? Note);

public record RejectTransferRequest(string? Reason);

/// <summary>CR-2026-09-10-31: one line of a gift's conversation.</summary>
public record GiftMessageDto(
    Guid MessageId,
    Guid TransferId,
    Guid AuthorMemberId,
    string AuthorDisplayName,
    string Direction,
    string Kind,
    string Body,
    DateTime SentAt,
    bool IsRead);

/// <summary>CR-2026-09-10-31: what a participant gets when they open a gift's conversation.</summary>
public record GiftThreadDto(
    Guid TransferId,
    string Status,
    bool CanReply,
    string? ClosedReason,
    IEnumerable<GiftMessageDto> Messages);

/// <summary>CR-2026-09-10-31: the body of a follow-up message a member adds to a gift.</summary>
public record PostGiftMessageRequest(string? Body);
