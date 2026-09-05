using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public interface IApprovalService
{
    /// <summary>
    /// Approves a pending plan and charges the brand <c>TargetQuantity</c> credits.
    /// When <paramref name="generateVouchers"/> is true the plan's voucher detail rows are
    /// materialized in the same request (after the approval commits); a generation failure does
    /// not roll back the approval — it is reported through <see cref="ApprovalResult.Warning"/>.
    /// </summary>
    Task<ApprovalResult> ApproveAsync(Guid planId, Guid approverId, Guid brandId, string approverRole, DateTime? publishDate, bool generateVouchers = false, CancellationToken cancellationToken = default);
    Task<ApprovalResult> RejectAsync(Guid planId, Guid approverId, Guid brandId, string approverRole, string reviewNotes, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VoucherReview>> GetReviewHistoryAsync(Guid planId, Guid brandId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pre-check for the approve form (point 1): can this plan's brand fund its approved quantity?
    /// Returns null when the plan is missing or not owned by the brand.
    /// </summary>
    Task<CreditFundingResult?> GetPlanFundingAsync(Guid planId, Guid brandId, CancellationToken cancellationToken = default);
}

public record ApprovalResult(bool Success, string? ErrorCode = null, string? ErrorMessage = null, VoucherPlanHeader? Plan = null, int GeneratedCount = 0, string? Warning = null);
