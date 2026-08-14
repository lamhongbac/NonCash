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
    private readonly ICreditPolicyService _policyService;
    private readonly ICurrentUserService _currentUser;

    public CreditsController(
        ICreditService creditService,
        ICreditPolicyService policyService,
        ICurrentUserService currentUser)
    {
        _creditService = creditService;
        _policyService = policyService;
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
    /// Returns a paginated credit batch history with optional filters — same scoping as balance.
    /// </summary>
    [HttpGet("batches")]
    public async Task<ActionResult<CreditBatchListResponse>> GetBatches(
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

        var filters = new CreditBatchFilters
        {
            BrandId = scopedBrandId,
            BatchType = Enum.TryParse<CreditBatchType>(type, true, out var t) ? t : null,
            FromDate = from,
            ToDate = to,
            Page = page,
            PageSize = pageSize
        };

        var result = await _creditService.GetBatchesAsync(filters, cancellationToken);
        var batches = result.Batches.Select(ToDto).ToList();

        return Ok(new CreditBatchListResponse(batches, result.TotalCount, result.Page, result.PageSize));
    }

    /// <summary>
    /// Returns a paginated per-voucher consumption history — same scoping as balance.
    /// </summary>
    [HttpGet("consumptions")]
    public async Task<ActionResult<CreditConsumptionListResponse>> GetConsumptions(
        [FromQuery] Guid? brandId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var scopedBrandId = ResolveBrandScope(brandId);
        if (scopedBrandId == null)
            return Forbid();

        var result = await _creditService.GetConsumptionsAsync(scopedBrandId.Value, page, pageSize, cancellationToken);

        var consumptions = result.Consumptions.Select(c => new CreditConsumptionDto(
            c.Id, c.BatchId, c.VoucherDetailId, c.Reference, c.CreatedAt)).ToList();

        return Ok(new CreditConsumptionListResponse(consumptions, result.TotalCount, result.Page, result.PageSize));
    }

    /// <summary>
    /// Returns the effective pricing policy for the brand (Brand → Group → Global → config fallback).
    /// </summary>
    [HttpGet("pricing")]
    public async Task<ActionResult<ResolvedPolicyResponse>> GetPricing(
        [FromQuery] Guid? brandId,
        CancellationToken cancellationToken)
    {
        var scopedBrandId = ResolveBrandScope(brandId);
        if (scopedBrandId == null)
            return Forbid();

        var policy = await _policyService.ResolveForBrandAsync(scopedBrandId.Value, cancellationToken);

        return Ok(new ResolvedPolicyResponse(
            policy.PolicyId,
            policy.Name,
            policy.Scope?.ToString(),
            policy.PricePerCreditVnd,
            policy.CreditExpiryMonths,
            policy.LowBalanceWarningPct,
            policy.ExpiryWarningDays,
            policy.AdjustmentApprovalThreshold));
    }

    /// <summary>
    /// Returns batches with remaining credits expiring within the given window (default 30 days).
    /// </summary>
    [HttpGet("expiring")]
    public async Task<ActionResult<CreditBatchListResponse>> GetExpiring(
        [FromQuery] Guid? brandId,
        [FromQuery] int withinDays = 30,
        CancellationToken cancellationToken = default)
    {
        var scopedBrandId = ResolveBrandScope(brandId);
        if (scopedBrandId == null)
            return Forbid();

        var batches = await _creditService.GetExpiringBatchesAsync(scopedBrandId.Value, withinDays, cancellationToken);
        var dtos = batches.Select(ToDto).ToList();

        return Ok(new CreditBatchListResponse(dtos, dtos.Count, 1, dtos.Count == 0 ? 1 : dtos.Count));
    }

    /// <summary>
    /// Records a credit purchase after the admin verified the bank money-in. Admin only.
    /// Price and expiry are snapshotted from the brand's resolved policy.
    /// </summary>
    [HttpPost("topup")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CreditBatchDto>> TopUp(
        [FromBody] CreditPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        if (request.BrandId == Guid.Empty)
            return BadRequest(new { error = "BrandId is required." });

        Guid? byUserId = Guid.TryParse(_currentUser.GetCurrentUserId(), out var uid) ? uid : null;

        try
        {
            var batch = await _creditService.CreatePurchaseAsync(
                request.BrandId, request.Amount, request.Reference, request.EvidenceImageUrl, byUserId, cancellationToken);

            return Ok(ToDto(batch));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static CreditBatchDto ToDto(CreditBatch b) => new(
        b.Id,
        b.BrandId,
        b.Brand?.Name,
        b.BatchType.ToString(),
        b.OriginalAmount,
        b.RemainingAmount,
        b.PricePerCreditVnd,
        b.TotalPaidVnd,
        b.ExpiresAt,
        b.EvidenceImageUrl,
        b.Reference,
        b.AdjustmentRequestId,
        b.CreatedBy,
        b.CreatedAt);

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
