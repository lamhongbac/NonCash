namespace NonCash.Core.Entities;

public enum RegistrationStatus
{
    Submitted,
    UnderReview,
    Approved,
    Rejected
}

public enum ContractStatus
{
    None,
    Sent,
    Signed
}

public class BusinessRegistrationRequest : BaseEntity
{
    public Guid BrandId { get; set; }
    public Guid SubmittedByUserId { get; set; }
    public DateTime SubmittedAt { get; set; }
    public RegistrationStatus Status { get; set; } = RegistrationStatus.Submitted;
    public string? ReviewNotes { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }

    // Contract workflow (Option A): policy is selected and contract is sent before approval.
    public Guid? WelcomePolicyTemplateId { get; set; }
    public ContractStatus ContractStatus { get; set; } = ContractStatus.None;
    public DateTime? ContractSentAt { get; set; }
    public string? ContractFileUrl { get; set; }

    public Brand? Brand { get; set; }
    public UserAccount? SubmittedBy { get; set; }
    public UserAccount? ReviewedBy { get; set; }
    public WelcomeGrantPolicyTemplate? WelcomePolicyTemplate { get; set; }
}
