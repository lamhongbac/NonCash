using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public class ApprovalService : IApprovalService
{
    private readonly IVoucherPlanRepository _planRepository;
    private readonly IRepository<VoucherReview> _reviewRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly INotificationService _notificationService;
    private readonly ICreditService _creditService;
    private readonly IVoucherGenerationService _generationService;

    private static readonly HashSet<string> AllowedRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Approver", "Admin", "BrandManager" };

    public ApprovalService(
        IVoucherPlanRepository planRepository,
        IRepository<VoucherReview> reviewRepository,
        IUserAccountRepository userAccountRepository,
        INotificationService notificationService,
        ICreditService creditService,
        IVoucherGenerationService generationService)
    {
        _planRepository = planRepository;
        _reviewRepository = reviewRepository;
        _userAccountRepository = userAccountRepository;
        _notificationService = notificationService;
        _creditService = creditService;
        _generationService = generationService;
    }

    public async Task<ApprovalResult> ApproveAsync(Guid planId, Guid approverId, Guid brandId, string approverRole, DateTime? publishDate, bool generateVouchers = false, CancellationToken cancellationToken = default)
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

        // Charge credits at approval (1 credit = 1 approved voucher), for every voucher type.
        // Idempotent per plan, so a retried approval after a transient failure is not double-charged.
        // Refuses (no state change) when the balance cannot fund the full approved quantity.
        var charged = await _creditService.TryConsumeForPlanAsync(
            plan.BrandId, plan.Id, plan.TargetQuantity, $"Plan approval {plan.Id}", cancellationToken);
        if (!charged)
            return new ApprovalResult(false, "InsufficientCredits", "Not enough credit, please top up to continue.");

        // AC1: Update plan
        plan.ApprovalStatus = ApprovalStatus.Approved;
        plan.ApproverId = approverId;
        if (publishDate.HasValue)
            plan.PublishDate = DateTime.SpecifyKind(publishDate.Value, DateTimeKind.Utc);

        // AC3: Insert review record
        var review = new VoucherReview
        {
            PlanId = planId,
            ApproverId = approverId,
            ReviewDate = DateTime.UtcNow,
            Decision = ReviewDecision.Approved,
            PublishDate = publishDate.HasValue ? DateTime.SpecifyKind(publishDate.Value, DateTimeKind.Utc) : null
        };

        _planRepository.Update(plan);
        await _reviewRepository.AddAsync(review, cancellationToken);
        await _reviewRepository.SaveChangesAsync(cancellationToken);

        await NotifyPlanReviewedAsync(plan, approved: true, reviewNotes: null, cancellationToken);

        // Optional: materialize the approved quota as voucher detail rows in the same request.
        // The approval is already committed, so a generation failure never rolls it back — the
        // brand still owns the paid quota and can run "Generate Vouchers" later.
        if (generateVouchers && plan.TargetQuantity > 0)
        {
            var generation = await _generationService.GenerateBatchAsync(plan.Id, plan.TargetQuantity, plan.BrandId, cancellationToken);
            if (generation.Success)
                return new ApprovalResult(true, Plan: plan, GeneratedCount: generation.GeneratedCount);

            return new ApprovalResult(
                true,
                Plan: plan,
                Warning: "Approved, but voucher generation failed — run Generate Vouchers to create them.");
        }

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

        await NotifyPlanReviewedAsync(plan, approved: false, reviewNotes: reviewNotes.Trim(), cancellationToken);
        return new ApprovalResult(true, Plan: plan);
    }

    private async Task NotifyPlanReviewedAsync(VoucherPlanHeader plan, bool approved, string? reviewNotes, CancellationToken cancellationToken)
    {
        try
        {
            var creator = await _userAccountRepository.GetByIdAsync(plan.CreatorId, cancellationToken);
            if (string.IsNullOrWhiteSpace(creator?.Email))
                return;

            await _notificationService.NotifyPlanReviewedAsync(new PlanReviewedNotification(
                creator.Email,
                plan.DisplayName ?? "(unnamed plan)",
                approved,
                reviewNotes,
                approved ? plan.PublishDate : null), cancellationToken);
        }
        catch
        {
            // Notifications must never fail the approval workflow.
        }
    }

    public async Task<IReadOnlyList<VoucherReview>> GetReviewHistoryAsync(Guid planId, Guid brandId, CancellationToken cancellationToken = default)
    {
        var plan = await _planRepository.GetByIdAsync(planId, cancellationToken);
        if (plan == null || plan.BrandId != brandId)
            return Array.Empty<VoucherReview>();

        var reviews = await _reviewRepository.FindAsync(r => r.PlanId == planId, cancellationToken);
        return reviews.OrderByDescending(r => r.ReviewDate).ToList();
    }

    public async Task<CreditFundingResult?> GetPlanFundingAsync(Guid planId, Guid brandId, CancellationToken cancellationToken = default)
    {
        var plan = await _planRepository.GetByIdAsync(planId, cancellationToken);
        if (plan == null || plan.BrandId != brandId)
            return null;

        // Same reusable check the approve action relies on (point 1 of 2).
        return await _creditService.EvaluatePlanFundingAsync(plan.BrandId, plan.TargetQuantity, cancellationToken);
    }
}
