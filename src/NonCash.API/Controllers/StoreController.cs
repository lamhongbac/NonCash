using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.Core.Interfaces;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/store")]
[Authorize]
public class StoreController : ControllerBase
{
    private readonly IPurchaseService _purchaseService;

    public StoreController(IPurchaseService purchaseService)
    {
        _purchaseService = purchaseService;
    }

    // AC1: Catalog of approved + published gift vouchers
    [HttpGet("vouchers")]
    [AllowAnonymous]
    public async Task<ActionResult<List<CatalogItemResponse>>> ListCatalog(CancellationToken cancellationToken)
    {
        var plans = await _purchaseService.ListCatalogAsync(cancellationToken);
        var items = plans.Select(p => new CatalogItemResponse(
            p.Id,
            p.FaceValue,
            p.NetValue,
            p.ValueType.ToString(),
            p.ValidFrom,
            p.ValidTo,
            p.ExpiryDate,
            p.ImageUrl,
            p.IconUrl,
            // Epic 8.1: Display fields
            p.CoverImageUrl,
            p.BrandColor,
            p.DisplayName,
            p.ShortDescription,
            p.TermsAndConditions,
            p.ValidDaysOfWeek
        )).ToList();
        return Ok(items);
    }
}

public record CatalogItemResponse(
    Guid PlanId,
    decimal FaceValue,
    decimal NetValue,
    string ValueType,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    DateTime ExpiryDate,
    string? ImageUrl,
    string? IconUrl,
    // Epic 8.1: Display fields
    string? CoverImageUrl,
    string? BrandColor,
    string? DisplayName,
    string? ShortDescription,
    string? TermsAndConditions,
    string? ValidDaysOfWeek);
