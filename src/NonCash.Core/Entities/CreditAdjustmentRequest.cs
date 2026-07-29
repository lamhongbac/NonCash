namespace NonCash.Core.Entities;

/// <summary>Lifecycle of a maker-checker adjustment request.</summary>
public enum AdjustmentStatus
{
    PendingApproval = 0,
    Approved = 1,
    Rejected = 2,
    /// <summary>Approved and the resulting credit batch has been created.</summary>
    Applied = 3
}

/// <summary>
/// Maker-checker credit adjustment request (Epic 10).
/// Admin (or FinancialController) requests; only FinancialController approves;
/// self-approval is forbidden. Approval matrix:
/// Correction/Clawback/Reinstatement → always; Grant/Compensation → when Amount ≥ threshold;
/// Purchase/WelcomeGrant never go through this flow.
/// </summary>
public class CreditAdjustmentRequest : BaseEntity
{
    public Guid BrandId { get; set; }

    /// <summary>Intent of the adjustment (Grant/Compensation/Correction/Clawback/Reinstatement).</summary>
    public CreditBatchType AdjustmentType { get; set; }

    /// <summary>Credit amount, always positive; Clawback direction comes from the type.</summary>
    public int Amount { get; set; }

    /// <summary>Batch being fixed — required for Correction/Clawback/Reinstatement.</summary>
    public Guid? RelatedBatchId { get; set; }

    /// <summary>Mandatory human-readable justification.</summary>
    public string ReasonText { get; set; } = string.Empty;

    /// <summary>Optional supporting note (ticket #, incident ref).</summary>
    public string? EvidenceNote { get; set; }

    /// <summary>Evidence image URL (MSA entity "credit_adjustments", uniqueCode = Id).</summary>
    public string? EvidenceImageUrl { get; set; }

    public AdjustmentStatus Status { get; set; } = AdjustmentStatus.PendingApproval;

    /// <summary>Whether this request needed FC approval (per matrix + threshold at request time).</summary>
    public bool RequiresApproval { get; set; }

    /// <summary>Threshold snapshot from the policy at request time.</summary>
    public int? ApprovalThreshold { get; set; }

    /// <summary>Policy in force when the request was made.</summary>
    public Guid? PolicyId { get; set; }

    public Guid RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; }

    /// <summary>FinancialController who approved/rejected. Must differ from RequestedBy.</summary>
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }

    /// <summary>Reviewer note — mandatory on Reject.</summary>
    public string? ReviewNote { get; set; }

    /// <summary>When the resulting batch was created (Status = Applied).</summary>
    public DateTime? AppliedAt { get; set; }

    // Navigation
    public Brand? Brand { get; set; }
    public CreditBatch? RelatedBatch { get; set; }
    public CreditPricingPolicy? Policy { get; set; }
}
