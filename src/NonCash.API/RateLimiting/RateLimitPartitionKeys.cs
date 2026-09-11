using System.Security.Cryptography;
using System.Text;

namespace NonCash.API.RateLimiting;

/// <summary>
/// Partition keys for the request rate limiter (CR-2026-09-06-04).
/// Limiter state lives for the whole process and can surface in diagnostics, so no key here
/// identifies a caller in the clear: API keys are hashed, and a missing client IP collapses to a
/// single shared bucket rather than throwing.
/// </summary>
internal static class RateLimitPartitionKeys
{
    /// <summary>Per client IP. Behind a reverse proxy this is the proxy's address unless
    /// ForwardedHeaders is configured, so every policy using it must tolerate one shared bucket.</summary>
    public static string ForClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "no-client-ip";

    /// <summary>Per outlet API key. Terminals that send no key share one bucket; ApiKeyMiddleware
    /// rejects them with 401 right after, so they never reach a controller.</summary>
    public static string ForPosApiKey(string? apiKey) =>
        string.IsNullOrEmpty(apiKey) ? "no-api-key" : Hash(apiKey);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
