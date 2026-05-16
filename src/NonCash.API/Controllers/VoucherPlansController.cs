using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.API.DTOs;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;
using System.Security.Claims;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/plans")]
[Authorize]
public class VoucherPlansController : ControllerBase
{
    private readonly IVoucherPlanService _planService;
    private readonly ICurrentUserService _currentUser;

    public VoucherPlansController(IVoucherPlanService planService, ICurrentUserService currentUser)
    {
        _planService = planService;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<ActionResult<PlanResponse>> Create(CreatePlanRequest request, CancellationToken cancellationToken)
    {
        var creatorIdStr = _currentUser.GetCurrentUserId();
        var brandId = _currentUser.GetCurrentBrandId();

        if (creatorIdStr == null || brandId == null || !Guid.TryParse(creatorIdStr, out var creatorId))
            return Unauthorized(new { error = "Invalid user context." });

        var dto = new CreatePlanDto(
            request.PlanDate,
            Enum.Parse<VoucherType>(request.VoucherType),
            Enum.Parse<VoucherValueType>(request.ValueType),
            request.FaceValue,
            request.NetValue,
            request.ExpiryDate,
            request.PublishDate,
            request.ValidFrom,
            request.ValidTo,
            request.TargetQuantity,
            request.Budget,
            request.ImageUrl,
            request.IconUrl,
            request.OutletIds
        );

        var result = await _planService.CreateAsync(dto, creatorId, brandId.Value, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = "Validation", message = result.ErrorMessage });

        return CreatedAtAction(nameof(GetById), new { id = result.Plan!.Id }, MapToResponse(result.Plan));
    }

    [HttpGet]
    public async Task<ActionResult<List<PlanResponse>>> List([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var brandId = _currentUser.GetCurrentBrandId();
        if (brandId == null)
            return Unauthorized(new { error = "Invalid user context." });

        ApprovalStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<ApprovalStatus>(status, out var s))
            statusFilter = s;

        var plans = await _planService.ListAsync(brandId.Value, statusFilter, cancellationToken);
        return Ok(plans.Select(MapToResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PlanResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var brandId = _currentUser.GetCurrentBrandId();
        if (brandId == null)
            return Unauthorized(new { error = "Invalid user context." });

        var plan = await _planService.GetByIdAsync(id, brandId.Value, cancellationToken);
        if (plan == null)
            return NotFound(new { error = "Plan not found." });

        return Ok(MapToResponse(plan));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PlanResponse>> Update(Guid id, UpdatePlanRequest request, CancellationToken cancellationToken)
    {
        var brandId = _currentUser.GetCurrentBrandId();
        if (brandId == null)
            return Unauthorized(new { error = "Invalid user context." });

        var dto = new UpdatePlanDto(
            request.PlanDate,
            Enum.Parse<VoucherType>(request.VoucherType),
            Enum.Parse<VoucherValueType>(request.ValueType),
            request.FaceValue,
            request.NetValue,
            request.ExpiryDate,
            request.PublishDate,
            request.ValidFrom,
            request.ValidTo,
            request.TargetQuantity,
            request.Budget,
            request.ImageUrl,
            request.IconUrl,
            request.OutletIds
        );

        var result = await _planService.UpdateDraftAsync(id, dto, brandId.Value, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = "Validation", message = result.ErrorMessage });

        return Ok(MapToResponse(result.Plan!));
    }

    private static PlanResponse MapToResponse(VoucherPlanHeader p) => new(
        p.Id,
        p.PlanDate,
        p.VoucherType.ToString(),
        p.ValueType.ToString(),
        p.FaceValue,
        p.NetValue,
        p.ExpiryDate,
        p.PublishDate,
        p.ValidFrom,
        p.ValidTo,
        p.TargetQuantity,
        p.Budget,
        p.TargetDistributed,
        p.TargetUsed,
        p.ApprovalStatus.ToString(),
        p.ImageUrl,
        p.IconUrl,
        p.PlanOutlets.Select(po => po.OutletId).ToList(),
        p.CreatedAt,
        p.UpdatedAt,
        p.VersionNumber,
        p.PreviousVersionId
    );
}
