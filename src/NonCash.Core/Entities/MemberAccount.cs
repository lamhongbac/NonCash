namespace NonCash.Core.Entities;

public enum MemberAccountStatus
{
    PendingActivation,
    Active,
    Locked
}

public class MemberAccount : BaseEntity
{
    public Guid CustomerId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public MemberAccountStatus Status { get; set; } = MemberAccountStatus.Active;

    public Customer? Customer { get; set; }
}
