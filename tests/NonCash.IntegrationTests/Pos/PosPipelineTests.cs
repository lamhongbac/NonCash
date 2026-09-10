using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.IntegrationTests.Pos;

/// <summary>
/// Pipeline-level guard for the POS X-API-Key gate (CR-2026-09-09-23). ApiKeyMiddleware was
/// written in CR-2026-09-07-18 but never registered in Program.cs, so /api/v1/pos/* ran
/// unauthenticated and every verify/lock died in the PosController cross-check with a null
/// pos.outlet_id. These tests boot the real pipeline; only a registered middleware can pass them.
/// </summary>
public class PosPipelineTests : IDisposable
{
    private const string ApiKey = "testkey1";
    private const string VoucherSecret = "pipeline-test-secret";
    private static readonly Guid BusinessId = Guid.Parse("05000000-0000-0000-0000-000000000001");
    private static readonly Guid BrandId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid OutletId = Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly Guid PlanId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid DetailId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    private static readonly Guid CreatorId = Guid.Parse("20000000-0000-0000-0000-000000000001");

    private readonly SqliteConnection _connection;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public PosPipelineTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection).Options;
        using (var ctx = new ApplicationDbContext(options))
        {
            ctx.Database.EnsureCreated();
            ctx.Businesses.Add(new Business
            {
                Id = BusinessId,
                BusinessName = "Pipeline Test Business",
                TaxCode = "TAX-TEST",
                Address = "Test",
                IsActive = true
            });
            ctx.Brands.Add(new Brand
            {
                Id = BrandId,
                BusinessId = BusinessId,
                Name = "Pipeline Test Brand",
                TaxCode = "TAX-TEST",
                Status = BrandStatus.Active
            });
            ctx.Outlets.Add(new Outlet
            {
                Id = OutletId,
                BrandId = BrandId,
                Name = "CMT8",
                Code = "CMT8",
                Status = OutletStatus.Active,
                ApiKeyPrefix = ApiKey
            });
            ctx.UserAccounts.Add(new UserAccount
            {
                Id = CreatorId,
                BrandId = BrandId,
                Username = "pipeline.creator",
                PasswordHash = "not-a-real-hash",
                FullName = "Pipeline Creator",
                Role = UserRole.BrandManager,
                Status = UserStatus.Active
            });
            ctx.VoucherPlanHeaders.Add(new VoucherPlanHeader
            {
                Id = PlanId,
                BrandId = BrandId,
                CreatorId = CreatorId,
                PlanDate = DateTime.UtcNow,
                VoucherType = VoucherType.Complimentary,
                ValueType = VoucherValueType.Value,
                FaceValue = 100000m,
                NetValue = 100000m,
                ExpiryDate = DateTime.UtcNow.AddYears(1),
                PublishDate = DateTime.UtcNow.AddDays(-1),
                ValidFrom = DateTime.UtcNow.AddDays(-1),
                ValidTo = DateTime.UtcNow.AddYears(1),
                TargetQuantity = 10,
                Budget = 1000000m,
                ApprovalStatus = ApprovalStatus.Approved,
                VersionNumber = 1,
                Scope = new VoucherScope { Brands = { BrandId }, Outlets = new List<Guid>() }
            });
            ctx.VoucherPlanDetails.Add(new VoucherPlanDetail
            {
                Id = DetailId,
                ParentId = PlanId,
                SerialNo = "VC-PIPE-00000001",
                VoucherCodeSecret = VoucherSecret,
                UsageStatus = UsageStatus.Pending
            });
            ctx.SaveChanges();
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                // AddDbContext(UseNpgsql) leaves provider services (IDatabaseProvider and friends)
                // in the container; removing only DbContextOptions<T> yields a two-provider error.
                // Sweep every registration owned by an EF/provider assembly, then re-add for SQLite.
                var efAssemblies = new HashSet<string>
                {
                    "Microsoft.EntityFrameworkCore",
                    "Microsoft.EntityFrameworkCore.Relational",
                    "Microsoft.EntityFrameworkCore.Sqlite",
                    "Npgsql.EntityFrameworkCore.PostgreSQL",
                };
                foreach (var descriptor in services
                             .Where(d => efAssemblies.Contains(d.ServiceType.Assembly.GetName().Name))
                             .ToList())
                {
                    services.Remove(descriptor);
                }
                services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection));
            });
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Verify_WithoutApiKey_IsRejectedByMiddleware()
    {
        var response = await PostVerifyAsync(apiKey: null, OutletId);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("MissingApiKey");
    }

    [Fact]
    public async Task Verify_WithUnknownApiKey_IsRejectedByMiddleware()
    {
        var response = await PostVerifyAsync("zzzzzzzz", OutletId);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("InvalidApiKey");
    }

    [Fact]
    public async Task Verify_WithKeyOfDifferentOutletThanBody_ReturnsTerminalKeyMismatch()
    {
        var response = await PostVerifyAsync(ApiKey, Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("TerminalKeyMismatch");
    }

    [Fact]
    public async Task Verify_WithMatchingKeyAndOutlet_PassesCrossCheck_AndReachesVoucherValidation()
    {
        var response = await PostVerifyAsync(ApiKey, OutletId, "VC-DOES-NOT-EXIST");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        // Serial-shaped input is classified by PosService, proving the request passed the
        // middleware and the controller cross-check.
        body.Should().Contain("InvalidCodeFormat");
        body.Should().NotContain("TerminalKeyMismatch");
    }

    [Fact]
    public async Task Verify_WithExpiredDynamicCode_ReturnsCodeExpired()
    {
        var response = await PostVerifyAsync(ApiKey, OutletId, MintCode(validitySeconds: -60));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("CodeExpired");
    }

    [Fact]
    public async Task Verify_WithFreshDynamicCode_ReturnsValid()
    {
        var response = await PostVerifyAsync(ApiKey, OutletId, MintCode());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"status\":\"Valid\"");
    }

    // IVoucherCodeService is scoped, so minting must happen inside a DI scope.
    private string MintCode(int validitySeconds = 120)
    {
        using var scope = _factory.Services.CreateScope();
        var codeService = scope.ServiceProvider.GetRequiredService<IVoucherCodeService>();
        return codeService.GenerateCode(DetailId, VoucherSecret, validitySeconds);
    }

    private Task<HttpResponseMessage> PostVerifyAsync(string? apiKey, Guid outletId, string code = "VC-DOES-NOT-EXIST")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/pos/verify")
        {
            Content = JsonContent.Create(new { voucherCode = code, outletId })
        };
        if (apiKey is not null)
        {
            request.Headers.Add("X-API-Key", apiKey);
        }
        return _client.SendAsync(request);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _connection.Dispose();
    }
}
