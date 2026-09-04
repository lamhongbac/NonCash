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

    // Epic 7.1: Cross-tenant sponsorship
    public Guid? SponsorBrandId { get; set; }

    // Epic 8.1: Display data model
    public string? CoverImageUrl { get; set; }
    public string? TermsAndConditions { get; set; }
    public string? BrandColor { get; set; }
    public string? DisplayName { get; set; }
    public string? ShortDescription { get; set; }
    public string? ValidDaysOfWeek { get; set; }

    // Navigation properties
    public UserAccount? Creator { get; set; }
    public UserAccount? Approver { get; set; }
    public Brand? Brand { get; set; }
    public Brand? SponsorBrand { get; set; }
    public VoucherPlanHeader? PreviousVersion { get; set; }

    /// <summary>
    /// Epic 3: Hierarchical applicability scope (companies/brands/outlets).
    /// An empty list at a level means "all under that level". Persisted as a single
    /// jsonb column ("scope") via a provider-agnostic EF Core value converter.
    /// </summary>
    public VoucherScope Scope { get; set; } = new();
}
