using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<MemberAuthResult> LoginMemberAsync(string username, string password, CancellationToken cancellationToken = default);
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
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
