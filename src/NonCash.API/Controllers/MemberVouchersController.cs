using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.Core.Interfaces;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/member/vouchers")]
[Authorize]
public class MemberVouchersController : ControllerBase
{
    private readonly ITransferService _transferService;
    private readonly IVoucherTransferService _voucherTransferService;

    public MemberVouchersController(
        ITransferService transferService,
        IVoucherTransferService voucherTransferService)
    {
        _transferService = transferService;
        _voucherTransferService = voucherTransferService;
    }

    private Guid GetCurrentMemberId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
    }

    // Story 5-1: Initiate peer-to-peer voucher transfer
    [HttpPost("{voucherId:guid}/initiate-transfer")]
    public async Task<ActionResult<InitiateTransferResponse>> InitiateTransfer(
        Guid voucherId,
        [FromBody] InitiateTransferRequest request,
        CancellationToken cancellationToken)
    {
        var senderId = GetCurrentMemberId();
        if (senderId == Guid.Empty)
            return Unauthorized(new { error = "Unauthorized", message = "Member identity is required." });

        if (request == null)
            return BadRequest(new { error = "Validation", message = "Request body is required." });

        if (string.IsNullOrWhiteSpace(request.RecipientPhone) && string.IsNullOrWhiteSpace(request.RecipientMemberId))
        {
            return BadRequest(new { error = "Validation", message = "RecipientPhone or RecipientMemberId is required." });
        }

        var result = await _voucherTransferService.InitiateAsync(
            senderId,
            voucherId,
            request.RecipientPhone,
            request.RecipientMemberId,
            request.Note,
            cancellationToken);

        if (!result.Success)
        {
            return result.ErrorCode switch
            {
                "VoucherNotFound" => NotFound(new { error = result.ErrorCode, message = result.ErrorMessage }),
                "NotOwned" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.ErrorCode, message = result.ErrorMessage }),
                "TransferAlreadyPending" => Conflict(new { error = result.ErrorCode, message = result.ErrorMessage }),
                _ => BadRequest(new { error = result.ErrorCode ?? "Validation", message = result.ErrorMessage })
            };
        }

        return Ok(new InitiateTransferResponse(result.TransferId!.Value, "PendingAcceptance"));
    }

    // AC1, AC3, AC4: Initiate batch transfer
    [HttpPost("transfer")]
    public async Task<ActionResult<TransferResponse>> Transfer(
        [FromBody] TransferRequest request,
        CancellationToken cancellationToken)
    {
        var currentMemberId = GetCurrentMemberId();
        if (currentMemberId == Guid.Empty)
            return Unauthorized(new { error = "Unauthorized", message = "Member identity is required." });

        if (request == null || request.FromMemberId == Guid.Empty)
            return BadRequest(new { error = "Validation", message = "FromMemberId is required." });

        if (request.FromMemberId != currentMemberId)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Forbidden", message = "FromMemberId does not match the authenticated member." });

        if (request.VoucherIds == null || request.VoucherIds.Count == 0)
            return BadRequest(new { error = "Validation", message = "VoucherIds list is required." });

        if (request.RecipientPhones == null || request.RecipientPhones.Count == 0)
            return BadRequest(new { error = "Validation", message = "RecipientPhones list is required." });

        var result = await _transferService.TransferAsync(
            request.FromMemberId,
            request.VoucherIds,
            request.RecipientPhones,
            cancellationToken);

        if (!result.Success)
        {
            return result.ErrorCode switch
            {
                "VoucherNotFound" => NotFound(new { error = result.ErrorCode, message = result.ErrorMessage }),
                "NotOwned" => StatusCode(403, new { error = result.ErrorCode, message = result.ErrorMessage }),
                _ => BadRequest(new { error = result.ErrorCode ?? "Validation", message = result.ErrorMessage, skipped = result.SkippedRecords })
            };
        }

        return Ok(new TransferResponse(
            result.TransferredCount,
            result.SkippedCount,
            result.SkippedRecords?.Select(s => new TransferSkippedDto(s.PhoneNumber, s.VoucherId, s.Reason)).ToList() ?? new List<TransferSkippedDto>()));
    }

    // AC5: Outgoing transfer history
    [HttpGet("transfer-history/{memberId:guid}")]
    public async Task<ActionResult<IEnumerable<TransferHistoryDto>>> History(Guid memberId, CancellationToken cancellationToken)
    {
        var currentMemberId = GetCurrentMemberId();
        if (currentMemberId == Guid.Empty)
            return Unauthorized(new { error = "Unauthorized", message = "Member identity is required." });

        if (memberId != currentMemberId)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Forbidden", message = "You can only view your own transfer history." });

        var history = await _transferService.GetOutgoingHistoryAsync(memberId, cancellationToken);
        var dto = history.Select(h => new TransferHistoryDto(h.VoucherId, h.SerialNo, h.RecipientPhone, h.TransferredAt));
        return Ok(dto);
    }
}

public record TransferRequest(Guid FromMemberId, List<Guid> VoucherIds, List<string> RecipientPhones);

public record TransferResponse(int TransferredCount, int SkippedCount, List<TransferSkippedDto> SkippedPhones);

public record TransferSkippedDto(string PhoneNumber, Guid VoucherId, string Reason);

public record TransferHistoryDto(Guid VoucherId, string SerialNo, string RecipientPhone, DateTime TransferredAt);

public record InitiateTransferRequest(string? RecipientPhone, string? RecipientMemberId, string? Note);

public record InitiateTransferResponse(Guid TransferId, string Status);
