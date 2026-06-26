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
            p.IconUrl
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
    string? IconUrl);
