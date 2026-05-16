using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public interface IVoucherPlanService
{
    Task<PlanResult> CreateAsync(CreatePlanDto dto, Guid creatorId, Guid brandId, CancellationToken cancellationToken = default);
    Task<PlanResult> UpdateDraftAsync(Guid planId, UpdatePlanDto dto, Guid brandId, CancellationToken cancellationToken = default);
    Task<VoucherPlanHeader?> GetByIdAsync(Guid id, Guid brandId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VoucherPlanHeader>> ListAsync(Guid brandId, ApprovalStatus? statusFilter, CancellationToken cancellationToken = default);
}

public record CreatePlanDto(
    DateTime PlanDate,
    VoucherType VoucherType,
    VoucherValueType ValueType,
    decimal FaceValue,
    decimal NetValue,
    DateTime ExpiryDate,
    DateTime PublishDate,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    int TargetQuantity,
    decimal Budget,
    string? ImageUrl,
    string? IconUrl,
    List<Guid> OutletIds
);

public record UpdatePlanDto(
    DateTime PlanDate,
    VoucherType VoucherType,
    VoucherValueType ValueType,
    decimal FaceValue,
    decimal NetValue,
    DateTime ExpiryDate,
    DateTime PublishDate,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    int TargetQuantity,
    decimal Budget,
    string? ImageUrl,
    string? IconUrl,
    List<Guid> OutletIds
);

public record PlanResult(bool Success, string? ErrorMessage = null, VoucherPlanHeader? Plan = null);

public class VoucherPlanService : IVoucherPlanService
{
    private readonly IVoucherPlanRepository _planRepository;
    private readonly IOutletRepository _outletRepository;

    public VoucherPlanService(
        IVoucherPlanRepository planRepository,
        IOutletRepository outletRepository)
    {
        _planRepository = planRepository;
        _outletRepository = outletRepository;
    }

    public async Task<PlanResult> CreateAsync(CreatePlanDto dto, Guid creatorId, Guid brandId, CancellationToken cancellationToken = default)
    {
        var validationError = ValidatePlan(dto.FaceValue, dto.NetValue, dto.ExpiryDate, dto.PublishDate, dto.ValidFrom, dto.ValidTo, dto.TargetQuantity);
        if (validationError != null)
            return new PlanResult(false, validationError);

        // Validate outlet ownership
        var outletError = await ValidateOutletOwnershipAsync(dto.OutletIds, brandId, cancellationToken);
        if (outletError != null)
            return new PlanResult(false, outletError);

        var plan = new VoucherPlanHeader
        {
            PlanDate = dto.PlanDate,
            CreatorId = creatorId,
            BrandId = brandId,
            VoucherType = dto.VoucherType,
            ValueType = dto.ValueType,
            FaceValue = dto.FaceValue,
            NetValue = dto.NetValue,
            ExpiryDate = dto.ExpiryDate,
            PublishDate = dto.PublishDate,
            ValidFrom = dto.ValidFrom,
            ValidTo = dto.ValidTo,
            TargetQuantity = dto.TargetQuantity,
            Budget = dto.Budget,
            ImageUrl = dto.ImageUrl,
            IconUrl = dto.IconUrl,
            ApprovalStatus = ApprovalStatus.Pending,
            TargetDistributed = 0,
            TargetUsed = 0
        };

        await _planRepository.AddAsync(plan, cancellationToken);
        await _planRepository.SaveChangesAsync(cancellationToken);

        // Add outlet associations
        foreach (var outletId in dto.OutletIds)
        {
            plan.PlanOutlets.Add(new PlanOutlet { PlanId = plan.Id, OutletId = outletId });
        }
        await _planRepository.SaveChangesAsync(cancellationToken);

        return new PlanResult(true, Plan: plan);
    }

    public async Task<PlanResult> UpdateDraftAsync(Guid planId, UpdatePlanDto dto, Guid brandId, CancellationToken cancellationToken = default)
    {
        var plan = await _planRepository.GetByIdWithOutletsAsync(planId, cancellationToken);
        if (plan == null)
            return new PlanResult(false, "Plan not found.");

        if (plan.BrandId != brandId)
            return new PlanResult(false, "You do not have access to this plan.");

        if (plan.ApprovalStatus != ApprovalStatus.Pending)
            return new PlanResult(false, "Only draft plans (Pending) can be edited.");

        var validationError = ValidatePlan(dto.FaceValue, dto.NetValue, dto.ExpiryDate, dto.PublishDate, dto.ValidFrom, dto.ValidTo, dto.TargetQuantity);
        if (validationError != null)
            return new PlanResult(false, validationError);

        var outletError = await ValidateOutletOwnershipAsync(dto.OutletIds, brandId, cancellationToken);
        if (outletError != null)
            return new PlanResult(false, outletError);

        // Update fields
        plan.PlanDate = dto.PlanDate;
        plan.VoucherType = dto.VoucherType;
        plan.ValueType = dto.ValueType;
        plan.FaceValue = dto.FaceValue;
        plan.NetValue = dto.NetValue;
        plan.ExpiryDate = dto.ExpiryDate;
        plan.PublishDate = dto.PublishDate;
        plan.ValidFrom = dto.ValidFrom;
        plan.ValidTo = dto.ValidTo;
        plan.TargetQuantity = dto.TargetQuantity;
        plan.Budget = dto.Budget;
        plan.ImageUrl = dto.ImageUrl;
        plan.IconUrl = dto.IconUrl;

        // Update outlet associations
        plan.PlanOutlets.Clear();
        foreach (var outletId in dto.OutletIds)
        {
            plan.PlanOutlets.Add(new PlanOutlet { PlanId = plan.Id, OutletId = outletId });
        }

        _planRepository.Update(plan);
        await _planRepository.SaveChangesAsync(cancellationToken);

        return new PlanResult(true, Plan: plan);
    }

    public async Task<VoucherPlanHeader?> GetByIdAsync(Guid id, Guid brandId, CancellationToken cancellationToken = default)
    {
        var plan = await _planRepository.GetByIdWithOutletsAsync(id, cancellationToken);
        if (plan != null && plan.BrandId != brandId)
            return null;
        return plan;
    }

    public async Task<IReadOnlyList<VoucherPlanHeader>> ListAsync(Guid brandId, ApprovalStatus? statusFilter, CancellationToken cancellationToken = default)
    {
        if (statusFilter.HasValue)
            return await _planRepository.ListByBrandAndStatusAsync(brandId, statusFilter.Value, cancellationToken);

        return await _planRepository.ListByBrandAsync(brandId, cancellationToken);
    }

    private static string? ValidatePlan(decimal faceValue, decimal netValue, DateTime expiryDate, DateTime publishDate, DateTime? validFrom, DateTime? validTo, int targetQuantity)
    {
        if (faceValue <= 0)
            return "Face value must be greater than 0.";
        if (netValue > faceValue)
            return "Net value cannot exceed face value.";
        if (expiryDate < publishDate)
            return "Expiry date must be on or after publish date.";
        if (targetQuantity <= 0)
            return "Target quantity must be greater than 0.";
        if (validFrom.HasValue && validTo.HasValue && validFrom >= validTo)
            return "Valid from must be before valid to.";
        return null;
    }

    private async Task<string?> ValidateOutletOwnershipAsync(List<Guid> outletIds, Guid brandId, CancellationToken cancellationToken)
    {
        if (outletIds.Count == 0) return null;

        var brandOutlets = await _outletRepository.ListByBrandAsync(brandId, cancellationToken);
        var brandOutletIds = brandOutlets.Select(o => o.Id).ToHashSet();

        var invalidOutlets = outletIds.Where(id => !brandOutletIds.Contains(id)).ToList();
        if (invalidOutlets.Count > 0)
            return "One or more selected outlets do not belong to your brand.";

        return null;
    }
}
