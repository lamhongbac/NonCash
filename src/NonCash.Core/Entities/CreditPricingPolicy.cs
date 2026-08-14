namespace NonCash.Core.Entities;

/// <summary>Scope a pricing policy applies to. Resolution priority: Brand > BrandGroup > Global.</summary>
public enum PolicyScope
{
    Global = 0,
    BrandGroup = 1,
    Brand = 2
}

/// <summary>
/// Versioned, time-bound credit pricing policy (Epic 10).
/// Stored in DB (not appsettings); the active policy for a brand is resolved
/// Brand → BrandGroup → Global, falling back to <c>CreditConfig</c> defaults.
/// </summary>
public class CreditPricingPolicy : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public PolicyScope Scope { get; set; } = PolicyScope.Global;

    /// <summary>Target group when Scope = BrandGroup.</summary>
    public Guid? BrandGroupId { get; set; }

    /// <summary>Target brand when Scope = Brand.</summary>
    public Guid? BrandId { get; set; }

    /// <summary>Flat unit price in VND for purchased credits (Model B: no volume tiers).</summary>
    public decimal PricePerCreditVnd { get; set; }

    /// <summary>Months until a purchased credit batch expires. Null = never expires.</summary>
    public int? CreditExpiryMonths { get; set; } = 12;

    /// <summary>Warn brand when balance falls below this % of last purchase. Null = no warning.</summary>
    public int? LowBalanceWarningPct { get; set; }

    /// <summary>Days before batch expiry to send the warning notification. Null = no warning.</summary>
    public int? ExpiryWarningDays { get; set; }

    /// <summary>
    /// Grant/Compensation adjustments at or above this amount require FinancialController approval.
    /// Null = every Grant/Compensation requires approval.
    /// </summary>
    public int? AdjustmentApprovalThreshold { get; set; }

    /// <summary>Policy effective period (UTC). EffectiveTo null = open-ended.</summary>
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Admin who created the policy version.</summary>
    public Guid? CreatedBy { get; set; }

    // Navigation
    public BrandGroup? BrandGroup { get; set; }
    public Brand? Brand { get; set; }
}
