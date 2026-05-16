namespace NonCash.Core.Entities;

public enum UsageStatus
{
    Pending,
    InUse,
    Complete
}

public class VoucherPlanDetail : BaseEntity
{
    public Guid ParentId { get; set; }
    public string SerialNo { get; set; } = string.Empty;
    public string VoucherCodeSecret { get; set; } = string.Empty;
    public Guid? MemberId { get; set; }
    public UsageStatus UsageStatus { get; set; } = UsageStatus.Pending;
    public DateTime? UsedDate { get; set; }

    // Story 4-2: POS lock fields (extension Option B)
    public Guid? LockId { get; set; }
    public DateTime? LockedAt { get; set; }
    public string? BillNumber { get; set; }
    public Guid? LockedOutletId { get; set; }

    // Navigation properties
    public VoucherPlanHeader? Parent { get; set; }
}
