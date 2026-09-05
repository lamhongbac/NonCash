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
    private readonly IRepository<VoucherPlanDetail> _detailRepository;

    public VoucherPlansController(
        IVoucherPlanService planService,
        ICurrentUserService currentUser,
        IRepository<VoucherPlanDetail> detailRepository)
    {
        _planService = planService;
        _currentUser = currentUser;
        _detailRepository = detailRepository;
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
            request.OutletIds,
            CoverImageUrl: request.CoverImageUrl,
            TermsAndConditions: request.TermsAndConditions,
            BrandColor: request.BrandColor,
            DisplayName: request.DisplayName,
            ShortDescription: request.ShortDescription,
            ValidDaysOfWeek: request.ValidDaysOfWeek
        );

        var result = await _planService.CreateAsync(dto, creatorId, brandId.Value, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = "Validation", message = result.ErrorMessage });

        var createdStock = await GetStockAsync(result.Plan!.Id, result.Plan.TargetQuantity, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Plan!.Id }, MapToResponse(result.Plan, createdStock));
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
        var responses = new List<PlanResponse>();
        foreach (var p in plans)
        {
            var stock = await GetStockAsync(p.Id, p.TargetQuantity, cancellationToken);
            responses.Add(MapToResponse(p, stock));
        }
        return Ok(responses);
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

        var stock = await GetStockAsync(plan.Id, plan.TargetQuantity, cancellationToken);
        return Ok(MapToResponse(plan, stock));
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
            request.OutletIds,
            CoverImageUrl: request.CoverImageUrl,
            TermsAndConditions: request.TermsAndConditions,
            BrandColor: request.BrandColor,
            DisplayName: request.DisplayName,
            ShortDescription: request.ShortDescription,
            ValidDaysOfWeek: request.ValidDaysOfWeek
        );

        var result = await _planService.UpdateDraftAsync(id, dto, brandId.Value, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = "Validation", message = result.ErrorMessage });

        var updatedStock = await GetStockAsync(result.Plan!.Id, result.Plan.TargetQuantity, cancellationToken);
        return Ok(MapToResponse(result.Plan!, updatedStock));
    }

    /// <summary>
    /// Derives voucher stock for a plan from its detail rows:
    /// Generated = all rows; Available = Pending + unassigned; AssignedUsed = Generated − Available;
    /// QuotaRemaining = TargetQuantity − Generated (never negative).
    /// </summary>
    private async Task<(int Generated, int AssignedUsed, int Available, int QuotaRemaining)> GetStockAsync(
        Guid planId, int targetQuantity, CancellationToken cancellationToken)
    {
        var generated = await _detailRepository.CountAsync(d => d.ParentId == planId, cancellationToken);
        var available = await _detailRepository.CountAsync(
            d => d.ParentId == planId && d.MemberId == null && d.UsageStatus == UsageStatus.Pending,
            cancellationToken);
        return (generated, generated - available, available, Math.Max(0, targetQuantity - generated));
    }

    private static PlanResponse MapToResponse(
        VoucherPlanHeader p,
        (int Generated, int AssignedUsed, int Available, int QuotaRemaining) stock) => new(
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
        p.Scope.Outlets,
        p.CreatedAt,
        p.UpdatedAt,
        p.VersionNumber,
        p.PreviousVersionId,
        CoverImageUrl: p.CoverImageUrl,
        TermsAndConditions: p.TermsAndConditions,
        BrandColor: p.BrandColor,
        DisplayName: p.DisplayName,
        ShortDescription: p.ShortDescription,
        ValidDaysOfWeek: p.ValidDaysOfWeek,
        Generated: stock.Generated,
        AssignedUsed: stock.AssignedUsed,
        Available: stock.Available,
        QuotaRemaining: stock.QuotaRemaining
    );
}
