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
        var jwtConfig = _configuration.GetSection("Jwt");
        var key = jwtConfig["Key"] ?? "noncash-dev-key-min-32-bytes-long!!";
        var issuer = jwtConfig["Issuer"] ?? "NonCash";
        var audience = jwtConfig["Audience"] ?? "NonCash.Users";
        var expiryHours = int.TryParse(jwtConfig["ExpiryHours"], out var h) ? h : 8;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("brand_id", user.BrandId?.ToString() ?? ""),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("full_name", user.FullName)
        };

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
