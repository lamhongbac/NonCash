namespace NonCash.Core.Entities;

public class VoucherUsage : BaseEntity
{
    public Guid VoucherId { get; set; }
    public Guid PosId { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public DateTime UsageDate { get; set; }
    public decimal AmountUsed { get; set; }

    // Epic 7.1: Cross-tenant settlement attribution
    public Guid? SponsorBrandId { get; set; }
    public Guid? RedeemBrandId { get; set; }

    // Navigation
    public VoucherPlanDetail? Voucher { get; set; }
    public Brand? SponsorBrand { get; set; }
    public Brand? RedeemBrand { get; set; }
}
