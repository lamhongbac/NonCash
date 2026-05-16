using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public class ApprovalService : IApprovalService
{
    private readonly IVoucherPlanRepository _planRepository;
    private readonly IRepository<VoucherReview> _reviewRepository;

    private static readonly HashSet<string> AllowedRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Approver", "Admin", "BrandManager" };

    public ApprovalService(
        IVoucherPlanRepository planRepository,
        IRepository<VoucherReview> reviewRepository)
    {
        _planRepository = planRepository;
        _reviewRepository = reviewRepository;
    }

    public async Task<ApprovalResult> ApproveAsync(Guid planId, Guid approverId, Guid brandId, string approverRole, DateTime? publishDate, CancellationToken cancellationToken = default)
    {
        // AC5: RBAC enforcement
        if (!AllowedRoles.Contains(approverRole))
            return new ApprovalResult(false, "Forbidden", "Only Approver, Admin, or BrandManager can approve plans.");

        var plan = await _planRepository.GetByIdAsync(planId, cancellationToken);
        if (plan == null)
            return new ApprovalResult(false, "NotFound", "Plan not found.");

        // NFR4: Multi-tenancy
        if (plan.BrandId != brandId)
            return new ApprovalResult(false, "Forbidden", "Plan does not belong to your brand.");

        // AC4: Single-level approval
        if (plan.ApprovalStatus != ApprovalStatus.Pending)
            return new ApprovalResult(false, "Conflict", $"Plan has already been {plan.ApprovalStatus}. No further approval allowed.");

        // AC1: Update plan
        plan.ApprovalStatus = ApprovalStatus.Approved;
        plan.ApproverId = approverId;
        if (publishDate.HasValue)
            plan.PublishDate = publishDate.Value;

        // AC3: Insert review record
        var review = new VoucherReview
        {
            PlanId = planId,
            ApproverId = approverId,
            ReviewDate = DateTime.UtcNow,
            Decision = ReviewDecision.Approved,
            PublishDate = publishDate
        };

        _planRepository.Update(plan);
        await _reviewRepository.AddAsync(review, cancellationToken);
        await _reviewRepository.SaveChangesAsync(cancellationToken);

        return new ApprovalResult(true, Plan: plan);
    }

    public async Task<ApprovalResult> RejectAsync(Guid planId, Guid approverId, Guid brandId, string approverRole, string reviewNotes, CancellationToken cancellationToken = default)
    {
        if (!AllowedRoles.Contains(approverRole))
            return new ApprovalResult(false, "Forbidden", "Only Approver, Admin, or BrandManager can reject plans.");

        // AC2: ReviewNotes required for rejection (min 10 chars)
        if (string.IsNullOrWhiteSpace(reviewNotes) || reviewNotes.Trim().Length < 10)
            return new ApprovalResult(false, "ValidationError", "Review notes are required and must be at least 10 characters when rejecting a plan.");

        var plan = await _planRepository.GetByIdAsync(planId, cancellationToken);
        if (plan == null)
            return new ApprovalResult(false, "NotFound", "Plan not found.");

        if (plan.BrandId != brandId)
            return new ApprovalResult(false, "Forbidden", "Plan does not belong to your brand.");

        if (plan.ApprovalStatus != ApprovalStatus.Pending)
            return new ApprovalResult(false, "Conflict", $"Plan has already been {plan.ApprovalStatus}. No further rejection allowed.");

        plan.ApprovalStatus = ApprovalStatus.Rejected;
        plan.ApproverId = approverId;

        var review = new VoucherReview
        {
            PlanId = planId,
            ApproverId = approverId,
            ReviewDate = DateTime.UtcNow,
            Decision = ReviewDecision.Rejected,
            ReviewNotes = reviewNotes.Trim()
        };

        _planRepository.Update(plan);
        await _reviewRepository.AddAsync(review, cancellationToken);
        await _reviewRepository.SaveChangesAsync(cancellationToken);

        return new ApprovalResult(true, Plan: plan);
    }

    public async Task<IReadOnlyList<VoucherReview>> GetReviewHistoryAsync(Guid planId, Guid brandId, CancellationToken cancellationToken = default)
    {
        var plan = await _planRepository.GetByIdAsync(planId, cancellationToken);
        if (plan == null || plan.BrandId != brandId)
            return Array.Empty<VoucherReview>();

        var reviews = await _reviewRepository.FindAsync(r => r.PlanId == planId, cancellationToken);
        return reviews.OrderByDescending(r => r.ReviewDate).ToList();
    }
}
