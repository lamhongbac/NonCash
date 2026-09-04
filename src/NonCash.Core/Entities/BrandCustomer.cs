namespace NonCash.Core.Entities;

public enum BrandCustomerSource
{
    Import,
    Manual,
    PromotionAuto,
    SelfPurchase,
    GiftingAuto,
    Transfer,
    Redemption
}

/// <summary>
/// Brand-customer relationship (docs/customer-action-matrix.md). Customers are global
/// platform identities; this mapping records which brand "knows" a customer, how the
/// relationship started, and carries the per-brand block and marketing consent.
/// </summary>
public class BrandCustomer : BaseEntity
{
    public Guid BrandId { get; set; }
    public Guid CustomerId { get; set; }

    /// <summary>How this relationship was first established. Never upgraded on re-link.</summary>
    public BrandCustomerSource Source { get; set; }

    /// <summary>UserAccount who created the link; null for system auto-links.</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>Per-brand block (matrix S1): blocks distribution/sale/transfer-in from this brand only. Never blocks redemption (P1).</summary>
    public bool IsBlocked { get; set; }

    public DateTime? BlockedAt { get; set; }

    /// <summary>Per-brand marketing consent (opt-out). Column reserved now, UI later.</summary>
    public bool MarketingOptOut { get; set; }

    // Navigation properties
    public Brand? Brand { get; set; }
    public Customer? Customer { get; set; }
}
