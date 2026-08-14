namespace NonCash.Core.Entities;

/// <summary>
/// Versioned, time-bound welcome-grant policy attached to a Business (Epic 10 refactor).
/// Welcome is a per-business commercial/contract term: every new brand a business
/// launches receives <see cref="WelcomeCredits"/> on activation, resolved from the
/// business's most recent active policy and falling back to <c>CreditConfig</c> defaults
/// when no policy is set. Stored per-business (not per-brand) so a negotiated deal
/// applies uniformly to each brand the business onboards.
/// </summary>
public class WelcomeGrantPolicy : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Business whose new brands this policy grants welcome credits to.</summary>
    public Guid BusinessId { get; set; }

    /// <summary>Free credits granted to each new brand under this business. 0 = none.</summary>
    public int WelcomeCredits { get; set; }

    /// <summary>Months until a welcome-grant batch expires. Null = never expires.</summary>
    public int? WelcomeCreditExpiryMonths { get; set; } = 12;

    /// <summary>Policy effective period (UTC). EffectiveTo null = open-ended.</summary>
    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Admin who created the policy version.</summary>
    public Guid? CreatedBy { get; set; }

    // Navigation
    public Business? Business { get; set; }
}
