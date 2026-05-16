namespace NonCash.Core.Entities;

public class VoucherUsage : BaseEntity
{
    public Guid VoucherId { get; set; }
    public Guid PosId { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public DateTime UsageDate { get; set; }
    public decimal AmountUsed { get; set; }

    // Navigation
    public VoucherPlanDetail? Voucher { get; set; }
}
