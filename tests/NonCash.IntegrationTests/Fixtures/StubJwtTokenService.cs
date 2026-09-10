using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.IntegrationTests.Fixtures;

/// <summary>
/// Minimal stub of IJwtTokenService for integration tests that construct services directly.
/// Returns fixed tokens and never validates.
/// </summary>
internal class StubJwtTokenService : IJwtTokenService
{
    public string GenerateToken(UserAccount user) => "fake-token";
    public string GenerateToken(UserAccount user, Guid outletId) => "fake-staff-token";
    public string GenerateToken(MemberAccount member) => "fake-member-token";
    public DateTime GetTokenExpiry() => DateTime.UtcNow.AddHours(1);
    public string GenerateMagicLinkToken(Guid memberAccountId) => "fake-magic-link-token";
    public string GenerateSignInLinkToken(Guid memberAccountId) => "fake-sign-in-link-token";
    public Guid? ValidateMagicLinkToken(string token) => null;
}
