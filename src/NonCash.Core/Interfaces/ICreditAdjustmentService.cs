using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

/// <summary>
/// Maker-checker credit adjustment workflow (Epic 10).
/// Admin/FinancialController requests; only FinancialController approves; no self-approval.
/// Matrix: Correction/Clawback/Reinstatement always need approval;
/// Grant/Compensation need approval at/above the policy threshold (or always when no threshold);
/// Purchase/WelcomeGrant are not adjustments.
/// </summary>
public interface ICreditAdjustmentService
{
    /// <summary>
    /// Creates an adjustment request. Auto-applies immediately (creates the batch)
    /// when the matrix says no approval is needed; otherwise notifies FinancialControllers.
    /// </summary>
    Task<CreditAdjustmentRequest> RequestAsync(CreditAdjustmentCommand command, CancellationToken cancellationToken = default);

    /// <summary>Approves a pending request (FinancialController, not the requester) and applies the batch.</summary>
    Task<CreditAdjustmentRequest> ApproveAsync(Guid requestId, Guid reviewerId, string? reviewNote, CancellationToken cancellationToken = default);

    /// <summary>Rejects a pending request (FinancialController, not the requester). Review note is mandatory.</summary>
    Task<CreditAdjustmentRequest> RejectAsync(Guid requestId, Guid reviewerId, string reviewNote, CancellationToken cancellationToken = default);

    Task<CreditAdjustmentRequest?> GetByIdAsync(Guid requestId, CancellationToken cancellationToken = default);

    Task<CreditAdjustmentResult> GetRequestsAsync(CreditAdjustmentFilters filters, CancellationToken cancellationToken = default);
}

public class CreditAdjustmentCommand
{
    public Guid BrandId { get; set; }
    public CreditBatchType AdjustmentType { get; set; }
    /// <summary>Always positive; Clawback direction comes from the type.</summary>
    public int Amount { get; set; }
    public Guid? RelatedBatchId { get; set; }
    public string ReasonText { get; set; } = string.Empty;
    public string? EvidenceNote { get; set; }
    public string? EvidenceImageUrl { get; set; }
    public Guid RequestedBy { get; set; }
}

public class CreditAdjustmentFilters
{
    public Guid? BrandId { get; set; }
    public AdjustmentStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public record CreditAdjustmentResult(
    IReadOnlyList<CreditAdjustmentRequest> Requests,
    int TotalCount,
    int Page,
    int PageSize);
