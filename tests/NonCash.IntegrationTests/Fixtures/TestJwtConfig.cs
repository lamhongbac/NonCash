using Microsoft.Extensions.Configuration;

namespace NonCash.IntegrationTests.Fixtures;

public static class TestJwtConfig
{
    public static IConfiguration Create()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Jwt:Key", "noncash-test-key-min-32-bytes-long!!" },
            { "Jwt:Issuer", "NonCash-Test" },
            { "Jwt:Audience", "NonCash-Test-Users" },
            { "Jwt:ExpiryHours", "1" }
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
    }
}
