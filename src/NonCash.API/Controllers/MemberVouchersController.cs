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

    public MemberVouchersController(ITransferService transferService)
    {
        _transferService = transferService;
    }

    // AC1, AC3, AC4: Initiate batch transfer
    [HttpPost("transfer")]
    public async Task<ActionResult<TransferResponse>> Transfer(
        [FromBody] TransferRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || request.FromMemberId == Guid.Empty)
            return BadRequest(new { error = "Validation", message = "FromMemberId is required." });

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
        var history = await _transferService.GetOutgoingHistoryAsync(memberId, cancellationToken);
        var dto = history.Select(h => new TransferHistoryDto(h.VoucherId, h.SerialNo, h.RecipientPhone, h.TransferredAt));
        return Ok(dto);
    }
}

public record TransferRequest(Guid FromMemberId, List<Guid> VoucherIds, List<string> RecipientPhones);

public record TransferResponse(int TransferredCount, int SkippedCount, List<TransferSkippedDto> SkippedPhones);

public record TransferSkippedDto(string PhoneNumber, Guid VoucherId, string Reason);

public record TransferHistoryDto(Guid VoucherId, string SerialNo, string RecipientPhone, DateTime TransferredAt);
