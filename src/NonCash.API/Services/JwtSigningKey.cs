using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace NonCash.API.Services;

/// <summary>
/// Single source for the HMAC signing key (CR-2026-09-06-13).
///
/// There is deliberately no code-resident default. Four call sites used to fall back to a literal
/// dev key committed in this repository, so any deployment that forgot to set <c>Jwt:Key</c> silently
/// signed and accepted tokens with a key anyone could read — and therefore forge an Admin token for.
/// Resolving the key in one place means the rule cannot be half-applied.
/// </summary>
public static class JwtSigningKey
{
    public const int MinimumBytes = 32;

    public static SymmetricSecurityKey Resolve(IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(HowToFix(
                "Jwt:Key is not configured, so the API cannot sign or verify tokens."));
        }

        var byteCount = Encoding.UTF8.GetByteCount(key);
        if (byteCount < MinimumBytes)
        {
            throw new InvalidOperationException(HowToFix(
                $"Jwt:Key is only {byteCount} bytes; at least {MinimumBytes} are required to sign tokens."));
        }

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }

    private static string HowToFix(string cause) =>
        $"{cause} Set a random value of at least {MinimumBytes} bytes — "
        + "in Development: dotnet user-secrets set \"Jwt:Key\" \"<value>\" --project src/NonCash.API/NonCash.API.csproj; "
        + "in pilot or production: the deployed appsettings.json or the Jwt__Key environment variable. "
        + "No built-in default is provided on purpose: a published fallback key lets anyone forge tokens.";
}
