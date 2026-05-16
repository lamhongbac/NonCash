using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public interface IPlanCloneService
{
    Task<CloneResult> CloneAsync(Guid rejectedPlanId, Guid creatorId, Guid brandId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VoucherPlanHeader>> GetVersionLineageAsync(Guid planId, Guid brandId, CancellationToken cancellationToken = default);
}

public record CloneResult(bool Success, Guid? NewPlanId = null, int VersionNumber = 0, string? ErrorCode = null, string? ErrorMessage = null);

public class PlanCloneService : IPlanCloneService
{
    private readonly IVoucherPlanRepository _planRepository;

    public PlanCloneService(IVoucherPlanRepository planRepository)
    {
        _planRepository = planRepository;
    }

    public async Task<CloneResult> CloneAsync(Guid rejectedPlanId, Guid creatorId, Guid brandId, CancellationToken cancellationToken = default)
    {
        var source = await _planRepository.GetByIdWithOutletsAsync(rejectedPlanId, cancellationToken);
        if (source == null)
            return new CloneResult(false, ErrorCode: "NotFound", ErrorMessage: "Source plan not found.");

        // NFR4: Multi-tenancy
        if (source.BrandId != brandId)
            return new CloneResult(false, ErrorCode: "Forbidden", ErrorMessage: "Plan does not belong to your brand.");

        // AC3: Cannot clone Approved plans
        if (source.ApprovalStatus == ApprovalStatus.Approved)
            return new CloneResult(false, ErrorCode: "CannotCloneApprovedPlan",
                ErrorMessage: "Cannot clone an Approved plan. Please create a new plan instead.");

        // AC1: Only Rejected plans can be cloned (Pending plans should be edited directly)
        if (source.ApprovalStatus != ApprovalStatus.Rejected)
            return new CloneResult(false, ErrorCode: "InvalidStatus",
                ErrorMessage: $"Only Rejected plans can be cloned. Current status: {source.ApprovalStatus}.");

        // AC1 + AC4: Deep clone scalar fields and PlanOutlets; nullify approval fields
        var clone = new VoucherPlanHeader
        {
            PlanDate = DateTime.UtcNow,
            CreatorId = creatorId,
            ApproverId = null,
            BrandId = source.BrandId,
            VoucherType = source.VoucherType,
            ImageUrl = source.ImageUrl,
            IconUrl = source.IconUrl,
            ValueType = source.ValueType,
            FaceValue = source.FaceValue,
            NetValue = source.NetValue,
            ExpiryDate = source.ExpiryDate,
            PublishDate = source.PublishDate,
            ValidFrom = source.ValidFrom,
            ValidTo = source.ValidTo,
            TargetQuantity = source.TargetQuantity,
            Budget = source.Budget,
            TargetDistributed = 0,
            TargetUsed = 0,
            ApprovalStatus = ApprovalStatus.Pending,
            PreviousVersionId = source.Id,
            VersionNumber = await ComputeNextVersionAsync(source, cancellationToken)
        };

        // AC4: Cascade clone of PlanOutlets via navigation
        if (source.PlanOutlets != null)
        {
            foreach (var outlet in source.PlanOutlets)
            {
                clone.PlanOutlets.Add(new PlanOutlet { OutletId = outlet.OutletId });
            }
        }

        await _planRepository.AddAsync(clone, cancellationToken);
        await _planRepository.SaveChangesAsync(cancellationToken);

        return new CloneResult(true, NewPlanId: clone.Id, VersionNumber: clone.VersionNumber);
    }

    public async Task<IReadOnlyList<VoucherPlanHeader>> GetVersionLineageAsync(Guid planId, Guid brandId, CancellationToken cancellationToken = default)
    {
        var plan = await _planRepository.GetByIdAsync(planId, cancellationToken);
        if (plan == null || plan.BrandId != brandId)
            return Array.Empty<VoucherPlanHeader>();

        // Walk back to root and collect lineage in both directions
        var lineage = new List<VoucherPlanHeader>();

        // Walk backward to root
        var current = plan;
        while (current != null)
        {
            lineage.Add(current);
            if (current.PreviousVersionId == null) break;
            current = await _planRepository.GetByIdAsync(current.PreviousVersionId.Value, cancellationToken);
        }

        // Walk forward (find children) by querying brand plans where PreviousVersionId == plan.Id (recursively)
        var brandPlans = await _planRepository.ListByBrandAsync(brandId, cancellationToken);
        var byPrev = brandPlans.Where(p => p.PreviousVersionId.HasValue)
            .GroupBy(p => p.PreviousVersionId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.VersionNumber).ToList());

        var queue = new Queue<Guid>();
        queue.Enqueue(plan.Id);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (byPrev.TryGetValue(id, out var children))
            {
                foreach (var child in children)
                {
                    if (lineage.All(x => x.Id != child.Id))
                    {
                        lineage.Add(child);
                        queue.Enqueue(child.Id);
                    }
                }
            }
        }

        return lineage.OrderBy(p => p.VersionNumber).ToList();
    }

    private async Task<int> ComputeNextVersionAsync(VoucherPlanHeader source, CancellationToken cancellationToken)
    {
        // Find lineage root and max version among siblings in the same lineage
        var rootId = await FindRootIdAsync(source, cancellationToken);
        var brandPlans = await _planRepository.ListByBrandAsync(source.BrandId, cancellationToken);

        var lineage = new HashSet<Guid> { rootId };
        bool changed;
        do
        {
            changed = false;
            foreach (var p in brandPlans)
            {
                if (p.PreviousVersionId.HasValue && lineage.Contains(p.PreviousVersionId.Value) && lineage.Add(p.Id))
                    changed = true;
            }
        } while (changed);

        var maxVersion = brandPlans.Where(p => lineage.Contains(p.Id)).Max(p => p.VersionNumber);
        return maxVersion + 1;
    }

    private async Task<Guid> FindRootIdAsync(VoucherPlanHeader plan, CancellationToken cancellationToken)
    {
        var current = plan;
        while (current.PreviousVersionId.HasValue)
        {
            var parent = await _planRepository.GetByIdAsync(current.PreviousVersionId.Value, cancellationToken);
            if (parent == null) break;
            current = parent;
        }
        return current.Id;
    }
}
