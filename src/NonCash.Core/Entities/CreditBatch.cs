namespace NonCash.Core.Entities;

/// <summary>How a credit batch entered the system.</summary>
public enum CreditBatchType
{
    /// <summary>Paid top-up: admin verified bank money-in first. Never needs approval.</summary>
    Purchase = 0,
    /// <summary>Automatic free credits on brand activation. Never needs approval.</summary>
    WelcomeGrant = 1,
    /// <summary>Manual free credits (promo, partnership). Approval when ≥ threshold.</summary>
    Grant = 2,
    /// <summary>Goodwill credits for a platform fault. Approval when ≥ threshold.</summary>
    Compensation = 3,
    /// <summary>Fix of a wrong earlier batch (adds credits). Always approved first.</summary>
    Correction = 4,
    /// <summary>Removal of wrongly-issued credits (negative batch). Always approved first.</summary>
    Clawback = 5,
    /// <summary>Re-adding credits from an expired/clawed-back batch. Always approved first.</summary>
    Reinstatement = 6
}

/// <summary>
/// One credit top-up = one batch (Epic 10). Each batch snapshots the unit price
/// and carries its own expiry. Balance = SUM(RemainingAmount) over non-expired batches;
/// consumption drains FIFO from the oldest non-expired batch.
/// </summary>
public class CreditBatch : BaseEntity
{
    public Guid BrandId { get; set; }

    /// <summary>Pricing policy in force when a Purchase/Adjustment batch was created (price/expiry snapshot source). Null for welcome grants.</summary>
    public Guid? PolicyId { get; set; }

    /// <summary>Welcome-grant policy in force when a WelcomeGrant batch was created. Null for non-welcome batches.</summary>
    public Guid? WelcomePolicyId { get; set; }

    public CreditBatchType BatchType { get; set; }

    /// <summary>Credits granted by this batch. Negative only for Clawback.</summary>
    public int OriginalAmount { get; set; }

    /// <summary>Credits still available in this batch (0..OriginalAmount).</summary>
    public int RemainingAmount { get; set; }

    /// <summary>Unit price snapshot at purchase time; 0 for free grants.</summary>
    public decimal PricePerCreditVnd { get; set; }

    /// <summary>Total VND actually paid (Purchase only); 0 otherwise.</summary>
    public decimal TotalPaidVnd { get; set; }

    /// <summary>When the remaining credits expire (UTC). Null = never.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>When the expiry warning was sent to the brand (dedupe marker). Null = not sent.</summary>
    public DateTime? ExpiryWarningSentAt { get; set; }

    /// <summary>Bank slip / evidence image URL (MSA entity "credit_batches", uniqueCode = Id).</summary>
    public string? EvidenceImageUrl { get; set; }

    /// <summary>Bank transfer ref or free-text reference.</summary>
    public string? Reference { get; set; }

    /// <summary>Set when the batch was produced by an approved adjustment request.</summary>
    public Guid? AdjustmentRequestId { get; set; }

    /// <summary>User who created the batch; null for system grants.</summary>
    public Guid? CreatedBy { get; set; }

    // Navigation
    public Brand? Brand { get; set; }
    public CreditPricingPolicy? Policy { get; set; }
    public WelcomeGrantPolicy? WelcomePolicy { get; set; }
    public CreditAdjustmentRequest? AdjustmentRequest { get; set; }
}
