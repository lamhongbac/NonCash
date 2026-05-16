namespace NonCash.Core.Entities;

public enum RegistrationStatus
{
    Submitted,
    UnderReview,
    Approved,
    Rejected
}

public class BrandRegistrationRequest : BaseEntity
{
    public Guid BrandId { get; set; }
    public Guid SubmittedByUserId { get; set; }
    public DateTime SubmittedAt { get; set; }
    public RegistrationStatus Status { get; set; } = RegistrationStatus.Submitted;
    public string? ReviewNotes { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }

    public Brand? Brand { get; set; }
    public UserAccount? SubmittedBy { get; set; }
    public UserAccount? ReviewedBy { get; set; }
}
