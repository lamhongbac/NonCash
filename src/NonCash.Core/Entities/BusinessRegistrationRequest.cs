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

/// <summary>
/// A business self-registration application. Until approval, no Business, Brand, or UserAccount
/// entities exist. Optional first-brand information may be supplied so the first Brand and
/// BrandManager user can be created automatically on approval.
/// </summary>
public class BusinessRegistrationRequest : BaseEntity
{
    // Business information captured at submission time.
    public string BusinessName { get; set; } = string.Empty;
    public string TaxCode { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string RepresentativeName { get; set; } = string.Empty;

    // Optional first-brand declaration. When supplied, a Brand and BrandManager user are
    // created on approval. When omitted, only the Business is created.
    public string? FirstBrandName { get; set; }
    public string? ManagerUsername { get; set; }
    public string? ManagerPasswordHash { get; set; }

    // Populated after approval when the real entities are created.
    public Guid? BusinessId { get; set; }
    public Guid? BrandId { get; set; }
    public Guid? SubmittedByUserId { get; set; }

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

    public Business? Business { get; set; }
    public Brand? Brand { get; set; }
    public UserAccount? SubmittedBy { get; set; }
    public UserAccount? ReviewedBy { get; set; }
    public WelcomeGrantPolicyTemplate? WelcomePolicyTemplate { get; set; }
}
