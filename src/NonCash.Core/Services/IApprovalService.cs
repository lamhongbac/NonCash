using NonCash.Core.Entities;

namespace NonCash.Core.Services;

public interface IApprovalService
{
    Task<ApprovalResult> ApproveAsync(Guid planId, Guid approverId, Guid brandId, string approverRole, DateTime? publishDate, CancellationToken cancellationToken = default);
    Task<ApprovalResult> RejectAsync(Guid planId, Guid approverId, Guid brandId, string approverRole, string reviewNotes, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VoucherReview>> GetReviewHistoryAsync(Guid planId, Guid brandId, CancellationToken cancellationToken = default);
}

public record ApprovalResult(bool Success, string? ErrorCode = null, string? ErrorMessage = null, VoucherPlanHeader? Plan = null);
