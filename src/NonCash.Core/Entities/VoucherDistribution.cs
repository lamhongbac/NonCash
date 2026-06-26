namespace NonCash.Core.Entities;

public enum DistributionMethod
{
    Sale,
    Promotion,
    Transfer
}

public class VoucherDistribution : BaseEntity
{
    public Guid VoucherId { get; set; }
    public Guid MemberId { get; set; }
    public DistributionMethod Method { get; set; }
    public DateTime DistributionDate { get; set; }

    // Navigation properties
    public VoucherPlanDetail? Voucher { get; set; }
    public MemberAccount? Member { get; set; }
}
