using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/settlements")]
[Authorize]
public class SettlementsController : ControllerBase
{
    private readonly ISettlementService _settlementService;
    private readonly ICurrentUserService _currentUser;

    public SettlementsController(ISettlementService settlementService, ICurrentUserService currentUser)
    {
        _settlementService = settlementService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Returns a paginated list of settlement entries with optional filters.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<SettlementLedgerResponse>> GetLedger(
        [FromQuery] Guid? sponsorBrandId,
        [FromQuery] Guid? redeemBrandId,
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var filters = new SettlementFilters
        {
            SponsorBrandId = sponsorBrandId,
            RedeemBrandId = redeemBrandId,
            Status = Enum.TryParse<SettlementStatus>(status, true, out var s) ? s : null,
            FromDate = from,
            ToDate = to,
            Page = page,
            PageSize = pageSize
        };

        var result = await _settlementService.GetLedgerAsync(filters, cancellationToken);

        var entries = result.Entries.Select(e => new SettlementEntryDto(
            e.Id,
            e.SponsorBrandId,
            e.SponsorBrand?.Name,
            e.IssuingBrandId,
            e.IssuingBrand?.Name,
            e.RedeemBrandId,
            e.RedeemBrand?.Name,
            e.RedeemOutletId,
            e.VoucherUsageId,
            e.FaceValue,
            e.Status.ToString(),
            e.SettledAt,
            e.SettledBy,
            e.CreatedAt
        )).ToList();

        return Ok(new SettlementLedgerResponse(entries, result.TotalCount, result.Page, result.PageSize));
    }

    /// <summary>
    /// Marks a pending settlement entry as settled.
    /// </summary>
    [HttpPut("{id:guid}/settle")]
    public async Task<ActionResult> MarkSettled(Guid id, CancellationToken cancellationToken)
    {
        var userIdString = _currentUser.GetCurrentUserId();
        if (!Guid.TryParse(userIdString, out var settledBy))
            return Unauthorized(new { error = "Invalid user context." });

        var success = await _settlementService.MarkSettledAsync(id, settledBy, cancellationToken);
        if (!success)
            return NotFound(new { error = "Entry not found or already settled." });

        return Ok(new { message = "Settlement marked as settled." });
    }

    /// <summary>
    /// Computes net amounts between all sponsor/redeemer brand pairs within a date range.
    /// </summary>
    [HttpGet("netting")]
    public async Task<ActionResult> GetNetting(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken cancellationToken = default)
    {
        var netting = await _settlementService.ComputeNettingAsync(from, to, cancellationToken);

        var rows = netting.Select(kvp => new NettingRowDto(
            kvp.Key.SponsorBrandId,
            kvp.Key.RedeemBrandId,
            kvp.Value
        )).ToList();

        return Ok(new NettingResponse(from, to, rows));
    }
}

public record SettlementEntryDto(
    Guid Id,
    Guid? SponsorBrandId,
    string? SponsorBrandName,
    Guid IssuingBrandId,
    string? IssuingBrandName,
    Guid? RedeemBrandId,
    string? RedeemBrandName,
    Guid? RedeemOutletId,
    Guid VoucherUsageId,
    decimal FaceValue,
    string Status,
    DateTime? SettledAt,
    Guid? SettledBy,
    DateTime CreatedAt);

public record SettlementLedgerResponse(
    List<SettlementEntryDto> Entries,
    int TotalCount,
    int Page,
    int PageSize);

public record NettingRowDto(
    Guid? SponsorBrandId,
    Guid? RedeemBrandId,
    decimal NetAmount);

public record NettingResponse(
    DateTime From,
    DateTime To,
    List<NettingRowDto> Rows);
