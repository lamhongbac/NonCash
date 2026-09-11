using Microsoft.Extensions.Configuration;

namespace NonCash.IntegrationTests.Fixtures;

public static class TestJwtConfig
{
    /// <summary>
    /// Test-only signing key. Program.cs now refuses to start without a Jwt:Key of at least 32 bytes,
    /// so the WebApplicationFactory fixtures must supply one through builder.UseSetting — they cannot
    /// rely on appsettings.json, whose tracked value is deliberately blank.
    /// </summary>
    public const string SigningKey = "noncash-test-key-min-32-bytes-long!!";

    public static IConfiguration Create()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Jwt:Key", SigningKey },
            { "Jwt:Issuer", "NonCash-Test" },
            { "Jwt:Audience", "NonCash-Test-Users" },
            { "Jwt:ExpiryHours", "1" },
            { "WebBaseUrl", "https://test.noncash.local" }
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
    }
}
