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
            i.ExpiresAt)));
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
            i.RespondedAt)));
    }

    // AC2: Accept transfer
    [HttpPost("{transferId:guid}/accept")]
    public async Task<ActionResult<TransferActionDto>> Accept(Guid transferId, CancellationToken cancellationToken)
    {
        var memberId = GetCurrentMemberId();
        if (memberId == Guid.Empty)
            return Unauthorized(new { error = "Unauthorized", message = "Member identity is required." });

        var result = await _transferService.AcceptAsync(transferId, memberId, cancellationToken);
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
    DateTime ExpiresAt);

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
    DateTime? RespondedAt);

public record TransferActionDto(string Status, Guid? VoucherId);

public record RejectTransferRequest(string? Reason);
