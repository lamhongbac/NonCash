namespace NonCash.Core.Entities;

public enum BrandStatus
{
    PendingActivation,
    Active,
    Suspended
}

public class Brand : BaseEntity
{
    public Guid BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TaxCode { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public BrandStatus Status { get; set; } = BrandStatus.Active;

    public Business Business { get; set; } = null!;
}
