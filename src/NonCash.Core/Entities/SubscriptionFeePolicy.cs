namespace NonCash.Core.Entities;

/// <summary>
/// Date-ranged subscription fee policy for the platform. The effective policy is resolved
/// by date; if no policy is in force, the contract falls back to CreditConfig values.
/// </summary>
public class SubscriptionFeePolicy : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Monthly or term subscription fee in VND. Ignored when <see cref="IsFree"/> is true.</summary>
    public decimal AmountVnd { get; set; }

    /// <summary>When true, the subscription fee is waived for the effective period.</summary>
    public bool IsFree { get; set; }

    /// <summary>Minimum commitment period in months (e.g. 12).</summary>
    public int MinimumCommitmentMonths { get; set; } = 12;

    /// <summary>Policy effective period (UTC). EffectiveTo null = open-ended.</summary>
    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}
