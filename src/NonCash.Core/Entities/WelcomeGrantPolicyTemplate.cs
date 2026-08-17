namespace NonCash.Core.Entities;

/// <summary>
/// Reusable welcome-grant policy template. Templates are created ahead of time and can be
/// assigned to many businesses. Exactly one template may be marked as the default; it is
/// used when a business has no explicit assignment at approval time.
/// </summary>
public class WelcomeGrantPolicyTemplate : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Free credits granted to each new brand under this template. 0 = none.</summary>
    public int WelcomeCredits { get; set; }

    /// <summary>Months until a welcome-grant batch expires. Null = never expires.</summary>
    public int? WelcomeCreditExpiryMonths { get; set; } = 12;

    public bool IsActive { get; set; } = true;

    /// <summary>True when this template should be used as the fallback for unassigned businesses.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Admin who created the template.</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>Admin who last updated the template.</summary>
    public Guid? UpdatedBy { get; set; }
}
