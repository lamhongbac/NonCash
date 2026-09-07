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
            user.FullName,
            outletId: null);

        return BuildToken(claims);
    }

    public string GenerateToken(UserAccount user, Guid outletId)
    {
        var claims = BuildClaims(
            user.Id.ToString(),
            user.Username,
            user.Role.ToString(),
            user.BrandId?.ToString() ?? "",
            "",
            user.FullName,
            outletId: outletId.ToString());

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
            member.FullName,
            outletId: null);

        return BuildToken(claims);
    }

    private static List<Claim> BuildClaims(string id, string username, string role, string brandId, string customerId, string fullName, string? outletId)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, id),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new(ClaimTypes.NameIdentifier, id),
            new("brand_id", brandId),
            new("customer_id", customerId),
            new(ClaimTypes.Role, role),
            new("full_name", fullName)
        };

        if (!string.IsNullOrEmpty(outletId))
        {
            claims.Add(new Claim("outlet_id", outletId));
        }

        return claims;
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

    /// <summary>CR-2026-09-07-19: Generate a magic-link JWT scoped to a MemberAccountId with 7-day TTL.</summary>
    public string GenerateMagicLinkToken(Guid memberAccountId)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, memberAccountId.ToString()),
            new("purpose", "magic_link")
        };
        return BuildTokenWithExpiry(claims, DateTime.UtcNow.AddDays(7));
    }

    /// <summary>CR-2026-09-07-19: Validate a magic-link JWT and return the MemberAccountId, or null if invalid.</summary>
    public Guid? ValidateMagicLinkToken(string token)
    {
        var jwtConfig = _configuration.GetSection("Jwt");
        var key = jwtConfig["Key"] ?? "noncash-dev-key-min-32-bytes-long!!";
        var issuer = jwtConfig["Issuer"] ?? "NonCash";
        var audience = jwtConfig["Audience"] ?? "NonCash.Users";

        var handler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };

        try
        {
            var principal = handler.ValidateToken(token, validationParameters, out _);
            var purposeClaim = principal.FindFirst("purpose");
            if (purposeClaim?.Value != "magic_link")
                return null;

            var subClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub)
                           ?? principal.FindFirst(ClaimTypes.NameIdentifier);
            if (subClaim == null || !Guid.TryParse(subClaim.Value, out var memberId))
                return null;

            return memberId;
        }
        catch
        {
            return null;
        }
    }

    private string BuildTokenWithExpiry(List<Claim> claims, DateTime expires)
    {
        var jwtConfig = _configuration.GetSection("Jwt");
        var key = jwtConfig["Key"] ?? "noncash-dev-key-min-32-bytes-long!!";
        var issuer = jwtConfig["Issuer"] ?? "NonCash";
        var audience = jwtConfig["Audience"] ?? "NonCash.Users";

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
