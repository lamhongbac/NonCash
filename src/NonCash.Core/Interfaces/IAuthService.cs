using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    /// <summary>CR-2026-09-09-27: customer sign-in. The identifier is the customer's phone number
    /// or email address — customers have no username.</summary>
    Task<MemberAuthResult> LoginMemberAsync(string identifier, string password, CancellationToken cancellationToken = default);
    /// <summary>CR-2026-09-07-18: POS staff login. username + storeCode + password.
    /// Returns a StoreStaff JWT scoped to the matching outlet. Generic failure message to prevent enumeration.</summary>
    Task<StaffAuthResult> LoginStaffAsync(string username, string storeCode, string password, CancellationToken cancellationToken = default);
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
    Task<ForgotPasswordResult> ForgotPasswordAsync(string usernameOrEmail, CancellationToken cancellationToken = default);
    /// <summary>CR-2026-09-09-27: customer self-service recovery — emails a 30-minute sign-in link
    /// to the account matching a phone number or email. Reports success whether or not a link was
    /// sent, so the endpoint cannot enumerate customers.</summary>
    Task<MemberSignInLinkResult> MemberForgotPasswordAsync(string identifier, CancellationToken cancellationToken = default);
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

/// <summary>CR-2026-09-09-27: outcome of a customer recovery request. <see cref="Message"/> is the
/// neutral confirmation shown to the customer; <see cref="ErrorMessage"/> is set only when the
/// identifier itself was unusable.</summary>
public record MemberSignInLinkResult(
    bool Success,
    string? Message = null,
    string? ErrorMessage = null
);

public record StaffAuthResult(
    bool Success,
    string? Token = null,
    DateTime? ExpiresAt = null,
    UserAccount? User = null,
    Guid? OutletId = null,
    string? OutletName = null,
    PosRedemptionMode OutletRedemptionMode = PosRedemptionMode.OneClick,
    string? ErrorMessage = null
);
