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

    // CR-2026-09-07-18 (C): optional operator attribution for POS clients (same trust level as cashier IDs).
    /// <summary>Client-reported POS terminal identifier. Optional — real POS integrations may omit it.</summary>
    public string? PosNo { get; set; }
    /// <summary>Client-reported cashier/operator identifier. Optional.</summary>
    public string? OperatorId { get; set; }

    // Navigation
    public VoucherPlanDetail? Voucher { get; set; }
    public Brand? SponsorBrand { get; set; }
    public Brand? RedeemBrand { get; set; }
}
