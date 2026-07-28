using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.API.DTOs;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/credits")]
[Authorize]
public class CreditsController : ControllerBase
{
    private readonly ICreditService _creditService;
    private readonly ICurrentUserService _currentUser;

    public CreditsController(ICreditService creditService, ICurrentUserService currentUser)
    {
        _creditService = creditService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Returns the current credit balance — own brand for BrandManager, any brand via ?brandId= for Admin.
    /// </summary>
    [HttpGet("balance")]
    public async Task<ActionResult<CreditBalanceResponse>> GetBalance(
        [FromQuery] Guid? brandId,
        CancellationToken cancellationToken)
    {
        var scopedBrandId = ResolveBrandScope(brandId);
        if (scopedBrandId == null)
            return Forbid();

        var balance = await _creditService.GetBalanceAsync(scopedBrandId.Value, cancellationToken);
        return Ok(new CreditBalanceResponse(scopedBrandId.Value, balance));
    }

    /// <summary>
    /// Returns a paginated credit ledger with optional filters — same scoping as balance.
    /// </summary>
    [HttpGet("ledger")]
    public async Task<ActionResult<CreditLedgerResponse>> GetLedger(
        [FromQuery] Guid? brandId,
        [FromQuery] string? type,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var scopedBrandId = ResolveBrandScope(brandId);
        if (scopedBrandId == null)
            return Forbid();

        var filters = new CreditLedgerFilters
        {
            BrandId = scopedBrandId,
            EntryType = Enum.TryParse<CreditEntryType>(type, true, out var t) ? t : null,
            FromDate = from,
            ToDate = to,
            Page = page,
            PageSize = pageSize
        };

        var result = await _creditService.GetLedgerAsync(filters, cancellationToken);

        var entries = result.Entries.Select(e => new CreditLedgerEntryDto(
            e.Id,
            e.BrandId,
            e.Brand?.Name,
            e.EntryType.ToString(),
            e.Amount,
            e.Reference,
            e.VoucherDetailId,
            e.CreatedBy,
            e.CreatedAt
        )).ToList();

        return Ok(new CreditLedgerResponse(entries, result.TotalCount, result.Page, result.PageSize));
    }

    /// <summary>
    /// Records a manual credit top-up (bank-transfer confirmation flow). Admin only.
    /// </summary>
    [HttpPost("topup")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CreditLedgerEntryDto>> TopUp(
        [FromBody] CreditTopUpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.BrandId == Guid.Empty)
            return BadRequest(new { error = "BrandId is required." });

        if (!Enum.TryParse<CreditEntryType>(request.Type, true, out var entryType) ||
            entryType == CreditEntryType.Consumption)
            return BadRequest(new { error = "Type must be Purchase, Grant, or Adjustment." });

        Guid? byUserId = Guid.TryParse(_currentUser.GetCurrentUserId(), out var uid) ? uid : null;

        try
        {
            var entry = await _creditService.TopUpAsync(
                request.BrandId, request.Amount, entryType, request.Reference, byUserId, cancellationToken);

            return Ok(new CreditLedgerEntryDto(
                entry.Id,
                entry.BrandId,
                null,
                entry.EntryType.ToString(),
                entry.Amount,
                entry.Reference,
                entry.VoucherDetailId,
                entry.CreatedBy,
                entry.CreatedAt));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Admin may target any brand via ?brandId=; other roles are scoped to their own brand.
    /// Returns null when no brand can be resolved (forbidden).
    /// </summary>
    private Guid? ResolveBrandScope(Guid? requestedBrandId)
    {
        if (_currentUser.IsInRole("Admin"))
            return requestedBrandId ?? _currentUser.GetCurrentBrandId();

        var ownBrandId = _currentUser.GetCurrentBrandId();
        if (ownBrandId == null)
            return null;

        // Non-admins may only query their own brand.
        if (requestedBrandId.HasValue && requestedBrandId.Value != ownBrandId.Value)
            return null;

        return ownBrandId;
    }
}
