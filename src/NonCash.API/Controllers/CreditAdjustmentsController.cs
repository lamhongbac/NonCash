using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.API.DTOs;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.API.Controllers;

/// <summary>
/// Maker-checker credit adjustment workflow (Epic 10).
/// Admin/FinancialController create requests; only FinancialController approves/rejects; no self-approval.
/// </summary>
[ApiController]
[Route("api/v1/credit-adjustments")]
[Authorize(Roles = "Admin,FinancialController")]
public class CreditAdjustmentsController : ControllerBase
{
    private readonly ICreditAdjustmentService _adjustmentService;
    private readonly ICurrentUserService _currentUser;

    public CreditAdjustmentsController(ICreditAdjustmentService adjustmentService, ICurrentUserService currentUser)
    {
        _adjustmentService = adjustmentService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<CreditAdjustmentListResponse>> GetRequests(
        [FromQuery] Guid? brandId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var filters = new CreditAdjustmentFilters
        {
            BrandId = brandId,
            Status = Enum.TryParse<AdjustmentStatus>(status, true, out var s) ? s : null,
            Page = page,
            PageSize = pageSize
        };

        var result = await _adjustmentService.GetRequestsAsync(filters, cancellationToken);
        var requests = result.Requests.Select(ToDto).ToList();

        return Ok(new CreditAdjustmentListResponse(requests, result.TotalCount, result.Page, result.PageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CreditAdjustmentDto>> GetRequest(Guid id, CancellationToken cancellationToken)
    {
        var request = await _adjustmentService.GetByIdAsync(id, cancellationToken);
        if (request == null)
            return NotFound();

        return Ok(ToDto(request));
    }

    /// <summary>
    /// Creates an adjustment request. Auto-applies when the approval matrix allows;
    /// otherwise stays PendingApproval and FinancialControllers are notified.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CreditAdjustmentDto>> Create(
        [FromBody] CreateAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.BrandId == Guid.Empty)
            return BadRequest(new { error = "BrandId is required." });

        if (!Enum.TryParse<CreditBatchType>(request.AdjustmentType, true, out var adjustmentType))
            return BadRequest(new { error = "AdjustmentType must be Grant, Compensation, Correction, Clawback, or Reinstatement." });

        if (!Guid.TryParse(_currentUser.GetCurrentUserId(), out var requestedBy))
            return Forbid();

        var command = new CreditAdjustmentCommand
        {
            BrandId = request.BrandId,
            AdjustmentType = adjustmentType,
            Amount = request.Amount,
            RelatedBatchId = request.RelatedBatchId,
            ReasonText = request.ReasonText,
            EvidenceNote = request.EvidenceNote,
            EvidenceImageUrl = request.EvidenceImageUrl,
            RequestedBy = requestedBy
        };

        try
        {
            var created = await _adjustmentService.RequestAsync(command, cancellationToken);
            return Ok(ToDto(created));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Approves a pending request and applies the credit batch. FinancialController only.</summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "FinancialController")]
    public async Task<ActionResult<CreditAdjustmentDto>> Approve(
        Guid id,
        [FromBody] ReviewAdjustmentRequest? request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.GetCurrentUserId(), out var reviewerId))
            return Forbid();

        try
        {
            var reviewed = await _adjustmentService.ApproveAsync(id, reviewerId, request?.Note, cancellationToken);
            return Ok(ToDto(reviewed));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Rejects a pending request. Review note is mandatory. FinancialController only.</summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "FinancialController")]
    public async Task<ActionResult<CreditAdjustmentDto>> Reject(
        Guid id,
        [FromBody] ReviewAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Note))
            return BadRequest(new { error = "A review note is mandatory when rejecting." });

        if (!Guid.TryParse(_currentUser.GetCurrentUserId(), out var reviewerId))
            return Forbid();

        try
        {
            var reviewed = await _adjustmentService.RejectAsync(id, reviewerId, request.Note, cancellationToken);
            return Ok(ToDto(reviewed));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static CreditAdjustmentDto ToDto(CreditAdjustmentRequest r) => new(
        r.Id,
        r.BrandId,
        r.Brand?.Name,
        r.AdjustmentType.ToString(),
        r.Amount,
        r.RelatedBatchId,
        r.ReasonText,
        r.EvidenceNote,
        r.EvidenceImageUrl,
        r.Status.ToString(),
        r.RequiresApproval,
        r.ApprovalThreshold,
        r.RequestedBy,
        r.RequestedAt,
        r.ReviewedBy,
        r.ReviewedAt,
        r.ReviewNote,
        r.AppliedAt);
}
