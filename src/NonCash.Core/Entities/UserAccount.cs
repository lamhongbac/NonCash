namespace NonCash.Core.Entities;

public enum UserRole
{
    Admin,
    BrandManager,
    Planner,
    Approver,
    /// <summary>Approves maker-checker credit adjustments (Epic 10). Cannot self-approve.</summary>
    FinancialController
}

public enum UserStatus
{
    PendingActivation,
    Active,
    Locked
}

public class UserAccount : BaseEntity
{
    public Guid? BrandId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;

    public Brand? Brand { get; set; }
}
