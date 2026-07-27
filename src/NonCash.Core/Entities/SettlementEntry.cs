namespace NonCash.Core.Entities;

/// <summary>
/// Tracks cross-tenant settlement obligations arising from voucher redemptions
/// where the sponsor brand differs from the redeeming brand.
/// </summary>
public class SettlementEntry : BaseEntity
{
    /// <summary>Brand that sponsored the voucher campaign (from VoucherPlanHeader.SponsorBrandId).</summary>
    public Guid? SponsorBrandId { get; set; }

    /// <summary>Brand that issued the voucher (owner of the plan).</summary>
    public Guid IssuingBrandId { get; set; }

    /// <summary>Brand at whose outlet the voucher was redeemed.</summary>
    public Guid? RedeemBrandId { get; set; }

    /// <summary>Outlet where the voucher was redeemed.</summary>
    public Guid? RedeemOutletId { get; set; }

    /// <summary>The VoucherUsage record that triggered this settlement entry.</summary>
    public Guid VoucherUsageId { get; set; }

    /// <summary>Face value of the voucher at time of redemption.</summary>
    public decimal FaceValue { get; set; }

    /// <summary>Settlement lifecycle status.</summary>
    public SettlementStatus Status { get; set; } = SettlementStatus.Pending;

    /// <summary>When the entry was marked as settled.</summary>
    public DateTime? SettledAt { get; set; }

    /// <summary>User/system identity that performed the settlement.</summary>
    public Guid? SettledBy { get; set; }

    // Navigation
    public Brand? SponsorBrand { get; set; }
    public Brand? IssuingBrand { get; set; }
    public Brand? RedeemBrand { get; set; }
    public Outlet? RedeemOutlet { get; set; }
    public VoucherUsage? VoucherUsage { get; set; }
}

public enum SettlementStatus
{
    Pending = 0,
    Settled = 1
}
