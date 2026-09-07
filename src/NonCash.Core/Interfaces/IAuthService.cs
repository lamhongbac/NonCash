using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<MemberAuthResult> LoginMemberAsync(string username, string password, CancellationToken cancellationToken = default);
    /// <summary>CR-2026-09-07-18: POS staff login. username + storeCode + password.
    /// Returns a StoreStaff JWT scoped to the matching outlet. Generic failure message to prevent enumeration.</summary>
    Task<StaffAuthResult> LoginStaffAsync(string username, string storeCode, string password, CancellationToken cancellationToken = default);
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
    Task<ForgotPasswordResult> ForgotPasswordAsync(string usernameOrEmail, CancellationToken cancellationToken = default);
    Task<AuthResult> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default);
    /// <summary>CR-2026-09-07-19: Passwordless login via magic-link token. Validates token and returns a session JWT.</summary>
    Task<MemberAuthResult> MagicLinkLoginAsync(string magicLinkToken, CancellationToken cancellationToken = default);
    /// <summary>CR-2026-09-07-19: Set password for a member (optional convenience after magic-link login).</summary>
    Task<bool> SetMemberPasswordAsync(Guid memberAccountId, string newPassword, CancellationToken cancellationToken = default);
}

public record AuthResult(
    bool Success,
    string? Token = null,
    DateTime? ExpiresAt = null,
    UserAccount? User = null,
    string? ErrorMessage = null
);

public record MemberAuthResult(
    bool Success,
    string? Token = null,
    DateTime? ExpiresAt = null,
    MemberAccount? Member = null,
    string? ErrorMessage = null
);

public record ForgotPasswordResult(
    bool Success,
    string? ErrorMessage = null
);

public record StaffAuthResult(
    bool Success,
    string? Token = null,
    DateTime? ExpiresAt = null,
    UserAccount? User = null,
    Guid? OutletId = null,
    string? ErrorMessage = null
);
