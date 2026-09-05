using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.Core.Interfaces;

namespace NonCash.API.Controllers;

/// <summary>
/// Read endpoints for batch distribution runs: the run history of a plan and the
/// per-recipient detail of one run. BrandManager/Admin only, brand-scoped.
/// </summary>
[ApiController]
[Authorize(Roles = "BrandManager,Admin")]
public class DistributionBatchesController : ControllerBase
{
    private readonly IDistributionBatchService _batchService;
    private readonly ICurrentUserService _currentUser;

    public DistributionBatchesController(IDistributionBatchService batchService, ICurrentUserService currentUser)
    {
        _batchService = batchService;
        _currentUser = currentUser;
    }

    [HttpGet("api/v1/plans/{planId:guid}/distribution-batches")]
    public async Task<ActionResult> ListBatches(Guid planId, CancellationToken cancellationToken)
    {
        var brandId = _currentUser.GetCurrentBrandId();
        if (brandId == null)
            return Unauthorized(new { error = "Invalid user context." });

        var batches = await _batchService.GetBatchesByPlanAsync(planId, brandId.Value, cancellationToken);
        return Ok(batches);
    }

    [HttpGet("api/v1/distribution-batches/{batchId:guid}")]
    public async Task<ActionResult> GetBatch(Guid batchId, CancellationToken cancellationToken)
    {
        var brandId = _currentUser.GetCurrentBrandId();
        if (brandId == null)
            return Unauthorized(new { error = "Invalid user context." });

        var detail = await _batchService.GetBatchDetailAsync(batchId, brandId.Value, cancellationToken);
        if (detail == null)
            return NotFound(); // Unknown id OR belongs to another brand

        return Ok(detail);
    }
}
