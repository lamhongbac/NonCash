using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<MemberAuthResult> LoginMemberAsync(string username, string password, CancellationToken cancellationToken = default);
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
    Task<ForgotPasswordResult> ForgotPasswordAsync(string usernameOrEmail, CancellationToken cancellationToken = default);
    Task<AuthResult> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default);
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
