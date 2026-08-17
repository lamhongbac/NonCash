namespace NonCash.Core.Entities;

/// <summary>
/// Per-business assignment/instance of a <see cref="WelcomeGrantPolicyTemplate"/>.
/// Every new brand a business launches receives <see cref="WelcomeCredits"/> on activation,
/// resolved from the business's most recent active assignment. If no assignment exists,
/// the platform's default template is used.
/// </summary>
public class WelcomeGrantPolicy : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Business whose new brands this assignment grants welcome credits to.</summary>
    public Guid BusinessId { get; set; }

    /// <summary>Source template. Null for legacy/override policies created before templates existed.</summary>
    public Guid? WelcomeGrantPolicyTemplateId { get; set; }

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

    /// <summary>Admin who last updated the policy version.</summary>
    public Guid? UpdatedBy { get; set; }

    // Navigation
    public Business? Business { get; set; }
    public WelcomeGrantPolicyTemplate? WelcomeGrantPolicyTemplate { get; set; }
}
