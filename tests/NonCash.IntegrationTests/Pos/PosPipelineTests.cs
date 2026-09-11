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
using NonCash.IntegrationTests.Fixtures;

namespace NonCash.IntegrationTests.Pos;

/// <summary>
/// Pipeline-level guard for the POS endpoints. These tests boot the real pipeline, so only
/// genuinely registered behaviour passes. Written originally for the X-API-Key gate
/// (CR-2026-09-09-23): ApiKeyMiddleware was written in CR-2026-09-07-18 but never registered in
/// Program.cs, so /api/v1/pos/* ran unauthenticated and every verify/lock died in the PosController
/// cross-check with a null pos.outlet_id. Also covers rollback outlet ownership
/// (CR-2026-09-06-01), the per-outlet rate budget (CR-2026-09-06-04) and the commit amount bound
/// (CR-2026-09-11-34).
/// </summary>
public class PosPipelineTests : IDisposable
{
    private const string ApiKey = "testkey1";
    private const string OtherApiKey = "testkey2";
    private const string VoucherSecret = "pipeline-test-secret";

    /// <summary>Must match the "pos-outlet" policy in Program.cs (CR-2026-09-06-04).</summary>
    private const int PosPermitLimit = 120;

    private static readonly Guid BusinessId = Guid.Parse("05000000-0000-0000-0000-000000000001");
    private static readonly Guid BrandId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid OutletId = Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherOutletId = Guid.Parse("70000000-0000-0000-0000-000000000002");
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
            ctx.Outlets.Add(new Outlet
            {
                Id = OtherOutletId,
                BrandId = BrandId,
                Name = "TN01",
                Code = "TN01",
                Status = OutletStatus.Active,
                ApiKeyPrefix = OtherApiKey
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
            // Program.cs resolves the signing key before Build() and throws without one, so the
            // fixture must supply it: the tracked appsettings value is deliberately blank.
            builder.UseSetting("Jwt:Key", TestJwtConfig.SigningKey);
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

    [Fact]
    public async Task Rollback_WithKeyOfAnotherOutlet_IsRefused_AndLeavesTheLockInPlace()
    {
        var lockId = await LockAsync(ApiKey, OutletId, "BILL-CMT8-001");

        var response = await PostRollbackAsync(OtherApiKey, lockId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RollbackResponse>();
        body!.Status.Should().Be("Invalid");
        body.Reason.Should().Be("OutletMismatch");

        var voucher = ReadVoucher();
        voucher.UsageStatus.Should().Be(UsageStatus.InUse);
        voucher.LockId.Should().Be(lockId);
        voucher.LockedOutletId.Should().Be(OutletId);
        voucher.BillNumber.Should().Be("BILL-CMT8-001");
    }

    [Fact]
    public async Task Rollback_WithKeyOfTheOwningOutlet_ReleasesTheVoucher()
    {
        var lockId = await LockAsync(ApiKey, OutletId, "BILL-CMT8-002");

        var response = await PostRollbackAsync(ApiKey, lockId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RollbackResponse>();
        body!.Status.Should().Be("Success");

        var voucher = ReadVoucher();
        voucher.UsageStatus.Should().Be(UsageStatus.Pending);
        voucher.LockId.Should().BeNull();
        voucher.LockedAt.Should().BeNull();
        voucher.LockedOutletId.Should().BeNull();
        voucher.BillNumber.Should().BeNull();
    }

    [Fact]
    public async Task Commit_WithAmountAboveFaceValue_IsRefused_AndLeavesTheVoucherLockedAndUnused()
    {
        var lockId = await LockAsync(ApiKey, OutletId, "BILL-CMT8-010");
        const string transactionId = "CMT8-BILL-CMT8-010-1";

        // Seeded FaceValue is 100000; a mistyped extra digit must not reach voucher_usages.
        var response = await PostCommitAsync(ApiKey, lockId, transactionId, 9999999m);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CommitResponse>();
        body!.Status.Should().Be("Invalid");
        body.Reason.Should().Be("AmountExceedsValue");

        var voucher = ReadVoucher();
        voucher.UsageStatus.Should().Be(UsageStatus.InUse);
        voucher.LockId.Should().Be(lockId);
        voucher.BillNumber.Should().Be("BILL-CMT8-010");
        ReadUsage(transactionId).Should().BeNull();
    }

    [Fact]
    public async Task Commit_WithAmountExactlyEqualToFaceValue_Succeeds()
    {
        var lockId = await LockAsync(ApiKey, OutletId, "BILL-CMT8-011");
        const string transactionId = "CMT8-BILL-CMT8-011-1";

        var response = await PostCommitAsync(ApiKey, lockId, transactionId, 100000m);

        var body = await response.Content.ReadFromJsonAsync<CommitResponse>();
        body!.Status.Should().Be("Success");
        ReadVoucher().UsageStatus.Should().Be(UsageStatus.Complete);
        ReadUsage(transactionId)!.AmountUsed.Should().Be(100000m);
    }

    [Fact]
    public async Task Commit_WithAmountBelowFaceValue_Succeeds_AndRecordsTheEnteredAmount()
    {
        var lockId = await LockAsync(ApiKey, OutletId, "BILL-CMT8-012");
        const string transactionId = "CMT8-BILL-CMT8-012-1";

        var response = await PostCommitAsync(ApiKey, lockId, transactionId, 40000m);

        var body = await response.Content.ReadFromJsonAsync<CommitResponse>();
        body!.Status.Should().Be("Success");

        // A partial amount still consumes the whole voucher today: single-use semantics. Locked
        // here so the move to balance-decrement instruments is a deliberate change, not an accident.
        ReadVoucher().UsageStatus.Should().Be(UsageStatus.Complete);
        ReadUsage(transactionId)!.AmountUsed.Should().Be(40000m);
    }

    [Fact]
    public async Task Verify_BeyondThePerOutletBudget_IsRejectedWith429()
    {
        // Verify is stateless and does not consume the code, so one code can serve the whole burst.
        var code = MintCode();

        HttpResponseMessage lastWithinBudget = null!;
        for (var attempt = 1; attempt <= PosPermitLimit; attempt++)
        {
            lastWithinBudget = await PostVerifyAsync(ApiKey, OutletId, code);
        }
        lastWithinBudget.StatusCode.Should().Be(HttpStatusCode.OK);

        var rejected = await PostVerifyAsync(ApiKey, OutletId, code);

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await rejected.Content.ReadAsStringAsync()).Should().Contain("too many voucher requests");
    }

    [Fact]
    public async Task Verify_WhenOneOutletIsThrottled_ASecondOutletStillSucceeds()
    {
        var code = MintCode();
        for (var attempt = 0; attempt <= PosPermitLimit; attempt++)
        {
            await PostVerifyAsync(ApiKey, OutletId, code);
        }

        var response = await PostVerifyAsync(OtherApiKey, OtherOutletId, code);

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

    private VoucherPlanDetail ReadVoucher()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return ctx.VoucherPlanDetails.AsNoTracking().Single(v => v.Id == DetailId);
    }

    private async Task<Guid> LockAsync(string apiKey, Guid outletId, string billNumber)
    {
        var response = await PostLockAsync(apiKey, outletId, MintCode(), billNumber);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LockResponse>();
        body!.Status.Should().Be("Locked");
        body.LockId.Should().NotBeNull();
        return body.LockId!.Value;
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

    private Task<HttpResponseMessage> PostLockAsync(string apiKey, Guid outletId, string code, string billNumber)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/pos/lock")
        {
            Content = JsonContent.Create(new { voucherCode = code, outletId, billNumber })
        };
        request.Headers.Add("X-API-Key", apiKey);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostRollbackAsync(string apiKey, Guid lockId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/pos/rollback")
        {
            Content = JsonContent.Create(new { lockId })
        };
        request.Headers.Add("X-API-Key", apiKey);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostCommitAsync(string apiKey, Guid lockId, string transactionId, decimal amountUsed)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/pos/commit")
        {
            Content = JsonContent.Create(new { lockId, transactionId, amountUsed })
        };
        request.Headers.Add("X-API-Key", apiKey);
        return _client.SendAsync(request);
    }

    private VoucherUsage? ReadUsage(string transactionId)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return ctx.VoucherUsages.AsNoTracking().SingleOrDefault(u => u.TransactionId == transactionId);
    }

    private sealed record LockResponse(string Status, string? Reason, Guid? LockId);

    private sealed record RollbackResponse(string Status, string? Reason, string? Message);

    private sealed record CommitResponse(string Status, string? Reason, string? Message);

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _connection.Dispose();
    }
}
