using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/plans/{planId:guid}")]
[Authorize]
public class PlanVersionsController : ControllerBase
{
    private readonly IPlanCloneService _cloneService;
    private readonly ICurrentUserService _currentUser;

    public PlanVersionsController(IPlanCloneService cloneService, ICurrentUserService currentUser)
    {
        _cloneService = cloneService;
        _currentUser = currentUser;
    }

    [HttpPost("clone")]
    public async Task<ActionResult> Clone(Guid planId, CancellationToken cancellationToken)
    {
        var brandId = _currentUser.GetCurrentBrandId();
        var userIdString = _currentUser.GetCurrentUserId();
        if (brandId == null || !Guid.TryParse(userIdString, out var userId))
            return Unauthorized(new { error = "Invalid user context." });

        var result = await _cloneService.CloneAsync(planId, userId, brandId.Value, cancellationToken);
        if (!result.Success)
        {
            return result.ErrorCode switch
            {
                "Forbidden" => StatusCode(403, new { error = result.ErrorCode, message = result.ErrorMessage }),
                "NotFound" => NotFound(new { error = result.ErrorCode, message = result.ErrorMessage }),
                "CannotCloneApprovedPlan" => BadRequest(new { error = result.ErrorCode, message = result.ErrorMessage }),
                _ => BadRequest(new { error = result.ErrorCode ?? "BadRequest", message = result.ErrorMessage })
            };
        }

        return CreatedAtAction(nameof(Clone), new { planId = result.NewPlanId },
            new { newPlanId = result.NewPlanId, versionNumber = result.VersionNumber });
    }

    [HttpGet("versions")]
    public async Task<ActionResult> GetVersions(Guid planId, CancellationToken cancellationToken)
    {
        var brandId = _currentUser.GetCurrentBrandId();
        if (brandId == null)
            return Unauthorized(new { error = "Invalid user context." });

        var lineage = await _cloneService.GetVersionLineageAsync(planId, brandId.Value, cancellationToken);
        var response = lineage.Select(p => new
        {
            p.Id,
            p.VersionNumber,
            p.PreviousVersionId,
            ApprovalStatus = p.ApprovalStatus.ToString(),
            p.PlanDate,
            p.PublishDate,
            p.FaceValue,
            p.TargetQuantity,
            p.Budget,
            IsCurrent = p.Id == planId
        });

        return Ok(response);
    }
}
