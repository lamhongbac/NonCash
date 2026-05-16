namespace NonCash.Core.Entities;

public enum ReviewDecision
{
    Approved,
    Rejected
}

public class VoucherReview : BaseEntity
{
    public Guid PlanId { get; set; }
    public Guid ApproverId { get; set; }
    public DateTime ReviewDate { get; set; }
    public string? ReviewNotes { get; set; }
    public ReviewDecision Decision { get; set; }
    public DateTime? PublishDate { get; set; }

    // Navigation properties
    public VoucherPlanHeader? Plan { get; set; }
    public UserAccount? Approver { get; set; }
}
