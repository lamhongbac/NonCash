namespace NonCash.Core.Entities;

/// <summary>
/// Named group of brands used as a pricing-policy target (Epic 10).
/// Lets admin apply one <see cref="CreditPricingPolicy"/> to many brands at once.
/// </summary>
public class BrandGroup : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<BrandGroupMember> Members { get; set; } = new List<BrandGroupMember>();
}

/// <summary>
/// Membership link between a <see cref="BrandGroup"/> and a <see cref="Brand"/>.
/// A brand may belong to multiple groups; policy resolution picks the most specific match.
/// </summary>
public class BrandGroupMember : BaseEntity
{
    public Guid BrandGroupId { get; set; }

    public Guid BrandId { get; set; }

    // Navigation
    public BrandGroup? BrandGroup { get; set; }
    public Brand? Brand { get; set; }
}
