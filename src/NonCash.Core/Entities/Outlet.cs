namespace NonCash.Core.Entities;

public enum OutletStatus
{
    Active,
    Closed
}

/// <summary>How cashiers at this outlet redeem vouchers on the POS terminal.</summary>
public enum PosRedemptionMode
{
    /// <summary>Single REDEEM action: the terminal runs lock+commit back-to-back. Market default.</summary>
    OneClick,

    /// <summary>Explicit Verify → Lock → Commit, keeping a cancel window between lock and commit.</summary>
    ThreeStep
}

public class Outlet : BaseEntity
{
    public Guid BrandId { get; set; }
    /// <summary>Short, human-typed store code used at POS app login (unique within a brand).</summary>
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public OutletStatus Status { get; set; } = OutletStatus.Active;
    public string? ApiKeyPrefix { get; set; }
    public PosRedemptionMode PosRedemptionMode { get; set; } = PosRedemptionMode.OneClick;

    public Brand Brand { get; set; } = null!;
}
