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

    // Epic 6.2: External member reference for Loyalty App integration
    public string? ExternalMemberId { get; set; }

    /// <summary>The batch run this distribution belongs to (promotion runs only; NULL for sale/transfer
    /// and for distributions recorded before batch tracking existed).</summary>
    public Guid? BatchId { get; set; }

    // Navigation properties
    public VoucherPlanDetail? Voucher { get; set; }
    public MemberAccount? Member { get; set; }
    public VoucherDistributionBatch? Batch { get; set; }
}
