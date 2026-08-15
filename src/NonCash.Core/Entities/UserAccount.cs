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
    public string? Email { get; set; }
    public UserRole Role { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;

    /// <summary>One-time token for password reset. Null when no reset is pending.</summary>
    public string? PasswordResetToken { get; set; }

    /// <summary>Expiry time for the password reset token.</summary>
    public DateTime? PasswordResetTokenExpiry { get; set; }

    public Brand? Brand { get; set; }
}
