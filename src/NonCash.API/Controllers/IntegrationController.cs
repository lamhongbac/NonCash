using Microsoft.AspNetCore.Mvc;
using NonCash.Core.Interfaces;

namespace NonCash.API.Controllers;

/// <summary>
/// API-key authenticated integration endpoints for external loyalty apps.
/// Authentication is handled by IntegrationApiKeyMiddleware (X-API-Key header).
/// HttpContext.Items["integration.partner_id"] and ["integration.brand_ids"] are set by middleware.
/// </summary>
[ApiController]
[Route("integration")]
public class IntegrationController : ControllerBase
{
    private readonly IPromotionService _promotionService;
    private readonly IVoucherEventPublisher _eventPublisher;
    private readonly IIntegrationPartnerService _partnerService;

    public IntegrationController(
        IPromotionService promotionService,
        IVoucherEventPublisher eventPublisher,
        IIntegrationPartnerService partnerService)
    {
        _promotionService = promotionService;
        _eventPublisher = eventPublisher;
        _partnerService = partnerService;
    }

    private (Guid PartnerId, List<Guid> BrandIds) GetPartnerContext()
    {
        var partnerId = HttpContext.Items["integration.partner_id"] as Guid? ?? Guid.Empty;
        var brandIds = HttpContext.Items["integration.brand_ids"] as List<Guid> ?? new();
        return (partnerId, brandIds);
    }

    // ===== Epic 6.2: Segment Distribution API =====

    /// <summary>
    /// Distributes vouchers to a segment of members identified by external_member_id.
    /// Reuses PromotionService logic with idempotency and blacklist enforcement.
    /// </summary>
    [HttpPost("distribute")]
    public async Task<ActionResult> Distribute(
        [FromBody] IntegrationDistributeRequest request,
        CancellationToken cancellationToken)
    {
        var (partnerId, brandIds) = GetPartnerContext();

        if (!brandIds.Contains(request.BrandId))
            return Forbid("Not authorized for this brand.");

        try
        {
            var result = await _promotionService.DistributeAsync(
                request.PlanId,
                request.BrandId,
                request.PhoneNumbers,
                cancellationToken);

            // Publish distribution events
            foreach (var phone in request.PhoneNumbers)
            {
                await _eventPublisher.PublishAsync(
                    "voucher.distributed",
                    null,
                    phone,
                    request.BrandId,
                    new { planId = request.PlanId, partnerId },
                    cancellationToken);
            }

            return Ok(new IntegrationDistributeResponse(
                result.DistributedCount,
                result.SkippedCount,
                result.SkippedRecords?.Select(s => new DistributionError(s.PhoneNumber, s.Reason)).ToList() ?? new()));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ===== Epic 6.3: Wallet & Event History API =====

    /// <summary>
    /// Returns the member's voucher wallet with display fields (from 8.1), scoped to partner's authorized brands.
    /// </summary>
    [HttpGet("member/{phone}/vouchers")]
    public async Task<ActionResult> GetMemberVouchers(
        string phone,
        CancellationToken cancellationToken)
    {
        var (partnerId, brandIds) = GetPartnerContext();

        var vouchers = await _promotionService.GetMemberVouchersByPhoneAsync(phone, brandIds, cancellationToken);

        var response = vouchers.Select(v => new IntegrationWalletItem(
            v.VoucherId,
            v.SerialNo,
            v.FaceValue,
            v.ValueType?.ToString() ?? "Value",
            v.ExpiryDate,
            v.UsageStatus?.ToString() ?? "Pending",
            v.ImageUrl,
            v.IconUrl,
            v.CoverImageUrl,
            v.BrandColor,
            v.DisplayName,
            v.ShortDescription,
            v.TermsAndConditions,
            v.BrandName
        )).ToList();

        return Ok(response);
    }

    /// <summary>
    /// Returns a unified event history for the member, aggregated from distributions, usages, and transfers.
    /// </summary>
    [HttpGet("member/{phone}/events")]
    public async Task<ActionResult> GetMemberEvents(
        string phone,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var (partnerId, brandIds) = GetPartnerContext();

        var events = await _promotionService.GetMemberEventsByPhoneAsync(phone, brandIds, limit, cancellationToken);

        var response = events.Select(e => new IntegrationEventItem(
            e.EventType,
            e.OccurredAt,
            e.VoucherId,
            e.SerialNo,
            e.BrandName,
            e.Details
        )).ToList();

        return Ok(response);
    }

    // ===== Epic 6.5: Campaign Performance API =====

    /// <summary>
    /// Returns campaign performance metrics: redemption rate, per-outlet breakdown.
    /// </summary>
    [HttpGet("campaigns/{planId:guid}/performance")]
    public async Task<ActionResult> GetCampaignPerformance(
        Guid planId,
        CancellationToken cancellationToken)
    {
        var (partnerId, brandIds) = GetPartnerContext();

        var performance = await _promotionService.GetCampaignPerformanceAsync(planId, brandIds, cancellationToken);
        if (performance == null)
            return NotFound(new { error = "Plan not found or not accessible." });

        return Ok(performance);
    }
}

// Request/Response DTOs
public record IntegrationDistributeRequest(
    Guid PlanId,
    Guid BrandId,
    IReadOnlyList<string> PhoneNumbers,
    Dictionary<string, string>? ExternalMemberIds);

public record IntegrationDistributeResponse(
    int DistributedCount,
    int SkippedCount,
    List<DistributionError> Errors);

public record DistributionError(string Identifier, string Reason);

public record IntegrationWalletItem(
    Guid VoucherId,
    string SerialNo,
    decimal FaceValue,
    string ValueType,
    DateTime? ExpiryDate,
    string UsageStatus,
    string? ImageUrl,
    string? IconUrl,
    string? CoverImageUrl,
    string? BrandColor,
    string? DisplayName,
    string? ShortDescription,
    string? TermsAndConditions,
    string? BrandName);

public record IntegrationEventItem(
    string EventType,
    DateTime OccurredAt,
    Guid? VoucherId,
    string? SerialNo,
    string? BrandName,
    string? Details);
