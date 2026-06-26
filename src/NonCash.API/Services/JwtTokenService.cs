using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.API.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(UserAccount user)
    {
        var claims = BuildClaims(
            user.Id.ToString(),
            user.Username,
            user.Role.ToString(),
            user.BrandId?.ToString() ?? "",
            "",
            user.FullName);

        return BuildToken(claims);
    }

    public string GenerateToken(MemberAccount member)
    {
        var claims = BuildClaims(
            member.Id.ToString(),
            member.Username,
            "Member",
            "",
            member.CustomerId.ToString(),
            member.FullName);

        return BuildToken(claims);
    }

    private static List<Claim> BuildClaims(string id, string username, string role, string brandId, string customerId, string fullName)
    {
        return new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, id),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new(ClaimTypes.NameIdentifier, id),
            new("brand_id", brandId),
            new("customer_id", customerId),
            new(ClaimTypes.Role, role),
            new("full_name", fullName)
        };
    }

    private string BuildToken(List<Claim> claims)
    {
        var jwtConfig = _configuration.GetSection("Jwt");
        var key = jwtConfig["Key"] ?? "noncash-dev-key-min-32-bytes-long!!";
        var issuer = jwtConfig["Issuer"] ?? "NonCash";
        var audience = jwtConfig["Audience"] ?? "NonCash.Users";
        var expiryHours = int.TryParse(jwtConfig["ExpiryHours"], out var h) ? h : 8;

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expiryHours),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public DateTime GetTokenExpiry()
    {
        var jwtConfig = _configuration.GetSection("Jwt");
        var expiryHours = int.TryParse(jwtConfig["ExpiryHours"], out var h) ? h : 8;
        return DateTime.UtcNow.AddHours(expiryHours);
    }
}
