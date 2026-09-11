using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NonCash.Infrastructure.Data;
using NonCash.IntegrationTests.Fixtures;

namespace NonCash.IntegrationTests.RateLimiting;

/// <summary>
/// CR-2026-09-06-04: the anonymous auth endpoints that had no limiter at all — login,
/// forgot-password, reset-password, magic-link — now share a per-client-IP "auth-recovery" bucket.
/// The partition matters as much as the limit: a global bucket would let one abuser lock out
/// password recovery for every user on the platform, so these tests drive two client IPs and
/// require them to be independent.
/// </summary>
public class AuthRecoveryRateLimitTests : IDisposable
{
    private const int PermitLimit = 5;
    private const string ClientA = "203.0.113.10";
    private const string ClientB = "203.0.113.11";
    private const string MagicLink = "/api/v1/auth/magic-link";
    private const string ForgotPassword = "/api/v1/auth/forgot-password";

    private readonly SqliteConnection _connection;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AuthRecoveryRateLimitTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection).Options;
        using (var ctx = new ApplicationDbContext(options))
        {
            ctx.Database.EnsureCreated();
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            // Program.cs resolves the signing key before Build() and throws without one, so the
            // fixture must supply it: the tracked appsettings value is deliberately blank.
            builder.UseSetting("Jwt:Key", TestJwtConfig.SigningKey);
            builder.ConfigureServices(services =>
            {
                // AddDbContext(UseNpgsql) leaves provider services in the container; removing only
                // DbContextOptions<T> yields a two-provider error. See PosPipelineTests.
                var efAssemblies = new HashSet<string>
                {
                    "Microsoft.EntityFrameworkCore",
                    "Microsoft.EntityFrameworkCore.Relational",
                    "Microsoft.EntityFrameworkCore.Sqlite",
                    "Npgsql.EntityFrameworkCore.PostgreSQL",
                };
                foreach (var descriptor in services
                             .Where(d => efAssemblies.Contains(d.ServiceType.Assembly.GetName().Name!))
                             .ToList())
                {
                    services.Remove(descriptor);
                }
                services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection));

                // TestServer gives every request the same connection info, so per-IP partitioning is
                // untestable without setting RemoteIpAddress ahead of UseRateLimiter. An IStartupFilter
                // wraps the application's own pipeline and therefore runs before it.
                services.AddSingleton<IStartupFilter, TestClientIpStartupFilter>();
            });
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task MagicLink_BeyondThePerIpBudget_IsRejectedWith429()
    {
        for (var attempt = 1; attempt <= PermitLimit; attempt++)
        {
            var response = await PostMagicLinkAsync(ClientA);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, $"attempt {attempt} is within budget");
        }

        var rejected = await PostMagicLinkAsync(ClientA);

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await rejected.Content.ReadAsStringAsync()).Should().Contain("Too many attempts");
    }

    [Fact]
    public async Task MagicLink_WhenOneClientIsThrottled_AnotherClientIsUnaffected()
    {
        for (var attempt = 0; attempt <= PermitLimit; attempt++)
        {
            await PostMagicLinkAsync(ClientA);
        }

        var response = await PostMagicLinkAsync(ClientB);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ForgotPassword_SharesTheRecoveryBucketWithMagicLink()
    {
        for (var attempt = 0; attempt < PermitLimit; attempt++)
        {
            await PostMagicLinkAsync(ClientA);
        }

        // The bucket is per IP across the whole recovery surface, not per endpoint: a caller who
        // spent the budget on magic-link cannot move to forgot-password and keep going.
        var response = await PostAsync(ForgotPassword, new { usernameOrEmail = "nobody@example.test" }, ClientA);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Too many attempts");
    }

    private Task<HttpResponseMessage> PostMagicLinkAsync(string clientIp) =>
        PostAsync(MagicLink, new { token = "" }, clientIp);

    private Task<HttpResponseMessage> PostAsync(string path, object body, string clientIp)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-Test-Client-Ip", clientIp);
        return _client.SendAsync(request);
    }

    private sealed class TestClientIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextStep) =>
            {
                if (IPAddress.TryParse(context.Request.Headers["X-Test-Client-Ip"].ToString(), out var ip))
                {
                    context.Connection.RemoteIpAddress = ip;
                }
                return nextStep();
            });
            next(app);
        };
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _connection.Dispose();
    }
}
