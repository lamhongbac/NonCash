using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/plans/{planId:guid}")]
[Authorize]
public class ApprovalsController : ControllerBase
{
    private readonly IApprovalService _approvalService;
    private readonly ICurrentUserService _currentUser;

    public ApprovalsController(IApprovalService approvalService, ICurrentUserService currentUser)
    {
        _approvalService = approvalService;
        _currentUser = currentUser;
    }

    [HttpPost("approve")]
    public async Task<ActionResult> Approve(Guid planId, [FromBody] ApproveRequest request, CancellationToken cancellationToken)
    {
        var (approverId, brandId, role) = GetUserContext();
        if (approverId == null || brandId == null || role == null)
            return Unauthorized(new { error = "Invalid user context." });

        var result = await _approvalService.ApproveAsync(planId, approverId.Value, brandId.Value, role, request.PublishDate, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("reject")]
    public async Task<ActionResult> Reject(Guid planId, [FromBody] RejectRequest request, CancellationToken cancellationToken)
    {
        var (approverId, brandId, role) = GetUserContext();
        if (approverId == null || brandId == null || role == null)
            return Unauthorized(new { error = "Invalid user context." });

        var result = await _approvalService.RejectAsync(planId, approverId.Value, brandId.Value, role, request.ReviewNotes, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("reviews")]
    public async Task<ActionResult> GetReviewHistory(Guid planId, CancellationToken cancellationToken)
    {
        var brandId = _currentUser.GetCurrentBrandId();
        if (brandId == null)
            return Unauthorized(new { error = "Invalid user context." });

        var reviews = await _approvalService.GetReviewHistoryAsync(planId, brandId.Value, cancellationToken);
        var response = reviews.Select(r => new
        {
            r.Id,
            r.PlanId,
            r.ApproverId,
            ApproverName = r.Approver?.FullName ?? "",
            r.ReviewDate,
            r.ReviewNotes,
            Decision = r.Decision.ToString(),
            r.PublishDate
        });

        return Ok(response);
    }

    private (Guid? approverId, Guid? brandId, string? role) GetUserContext()
    {
        var userIdString = _currentUser.GetCurrentUserId();
        var brandId = _currentUser.GetCurrentBrandId();
        var role = _currentUser.GetCurrentUserRole();

        if (!Guid.TryParse(userIdString, out var approverId))
            return (null, brandId, role);

        return (approverId, brandId, role);
    }

    private ActionResult ToActionResult(ApprovalResult result)
    {
        if (result.Success)
        {
            var plan = result.Plan!;
            return Ok(new
            {
                plan.Id,
                plan.ApprovalStatus,
                plan.ApproverId,
                plan.PublishDate
            });
        }

        return result.ErrorCode switch
        {
            "Forbidden" => StatusCode(403, new { error = result.ErrorCode, message = result.ErrorMessage }),
            "NotFound" => NotFound(new { error = result.ErrorCode, message = result.ErrorMessage }),
            "Conflict" => Conflict(new { error = result.ErrorCode, message = result.ErrorMessage }),
            _ => BadRequest(new { error = result.ErrorCode ?? "BadRequest", message = result.ErrorMessage })
        };
    }
}

public record ApproveRequest(DateTime? PublishDate);
public record RejectRequest(string ReviewNotes);
