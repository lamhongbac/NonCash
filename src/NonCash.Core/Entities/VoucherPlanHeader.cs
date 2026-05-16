namespace NonCash.Core.Entities;

public enum VoucherType
{
    Complimentary,
    Gift
}

public enum VoucherValueType
{
    Value,
    Percentage
}

public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected
}

public class VoucherPlanHeader : BaseEntity
{
    public DateTime PlanDate { get; set; }
    public Guid CreatorId { get; set; }
    public Guid? ApproverId { get; set; }
    public Guid BrandId { get; set; }
    public VoucherType VoucherType { get; set; }
    public string? ImageUrl { get; set; }
    public string? IconUrl { get; set; }
    public VoucherValueType ValueType { get; set; }
    public decimal FaceValue { get; set; }
    public decimal NetValue { get; set; }
    public DateTime ExpiryDate { get; set; }
    public DateTime PublishDate { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public int TargetQuantity { get; set; }
    public decimal Budget { get; set; }
    public int TargetDistributed { get; set; }
    public int TargetUsed { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;

    // Versioning fields (Story 2-4)
    public Guid? PreviousVersionId { get; set; }
    public int VersionNumber { get; set; } = 1;

    // Navigation properties
    public UserAccount? Creator { get; set; }
    public UserAccount? Approver { get; set; }
    public Brand? Brand { get; set; }
    public VoucherPlanHeader? PreviousVersion { get; set; }
    public ICollection<PlanOutlet> PlanOutlets { get; set; } = new List<PlanOutlet>();
}

public class PlanOutlet
{
    public Guid PlanId { get; set; }
    public Guid OutletId { get; set; }

    public VoucherPlanHeader? Plan { get; set; }
    public Outlet? Outlet { get; set; }
}
