namespace NonCash.Core.Entities;

public enum OutletStatus
{
    Active,
    Closed
}

public class Outlet : BaseEntity
{
    public Guid BrandId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public OutletStatus Status { get; set; } = OutletStatus.Active;
    public string? ApiKeyPrefix { get; set; }

    public Brand Brand { get; set; } = null!;
}
