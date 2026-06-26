using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(UserAccount user);
    string GenerateToken(MemberAccount member);
    DateTime GetTokenExpiry();
}
