using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(UserAccount user);
    /// <summary>CR-2026-09-07-18: token for a StoreStaff login session, scoped to a specific outlet.</summary>
    string GenerateToken(UserAccount user, Guid outletId);
    string GenerateToken(MemberAccount member);
    DateTime GetTokenExpiry();

    /// <summary>CR-2026-09-07-19: Generate a magic-link token for passwordless member access (7-day TTL).</summary>
    string GenerateMagicLinkToken(Guid memberAccountId);
    /// <summary>CR-2026-09-07-19: Validate a magic-link token and return the member account ID, or null if invalid/expired.</summary>
    Guid? ValidateMagicLinkToken(string token);
}
