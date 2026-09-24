using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NonCash.API.Controllers;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;
using NonCash.Infrastructure.Data;
using NonCash.Infrastructure.Repositories;
using NonCash.IntegrationTests.Fixtures;

namespace NonCash.IntegrationTests.Controllers;

/// <summary>
/// CR-2026-09-06-06: mint-on-tap. The wallet list must not mint codes eagerly; a member mints a
/// code on demand, only for their own usable voucher. Uses SQLite in-memory with the real
/// Repository&lt;VoucherPlanDetail&gt; and the real VoucherCodeService so the minted token is
/// verifiable end to end.
/// </summary>
public class MembersControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _context;
    private readonly Repository<VoucherPlanDetail> _detailRepository;

    private readonly Guid _memberId = Guid.NewGuid();
    private readonly Guid _otherMemberId = Guid.NewGuid();

    public MembersControllerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();
        _detailRepository = new Repository<VoucherPlanDetail>(_context);
    }

    private VoucherPlanDetail SeedDetail(Guid memberId, UsageStatus status, string serialNo, string secret)
    {
        var detail = new VoucherPlanDetail
        {
            ParentId = Guid.NewGuid(), // no plan header seeded; the list tolerates a missing plan
            SerialNo = serialNo,
            VoucherCodeSecret = secret,
            MemberId = memberId,
            UsageStatus = status,
            UsedDate = status == UsageStatus.Complete ? DateTime.UtcNow : null
        };
        _context.VoucherPlanDetails.Add(detail);
        _context.SaveChanges();
        return detail;
    }

    private MembersController CreateController(Guid? identityId, IVoucherCodeService? codeService = null)
    {
        var controller = new MembersController(
            _detailRepository,
            new StubPlanRepository(_context),
            codeService ?? new VoucherCodeService(),
            null!,
            null!);

        if (identityId.HasValue)
        {
            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, identityId.Value.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };
        }

        return controller;
    }

    // ----- list: no eager mint -----

    [Fact]
    public async Task GetMyVouchers_ReturnsRowsWithNullCode_AndNeverMints()
    {
        SeedDetail(_memberId, UsageStatus.Pending, "VC-M-00000001", "secret-1");
        var counting = new CountingVoucherCodeService();
        var controller = CreateController(_memberId, counting);

        var result = await controller.GetMyVouchers(_memberId, CancellationToken.None);

        var rows = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<IEnumerable<MemberVoucherResponse>>().Subject
            .ToList();
        rows.Should().ContainSingle();
        rows[0].VoucherCode.Should().BeNull();
        counting.GenerateCodeCalls.Should().Be(0);
    }

    // ----- mint endpoint -----

    [Fact]
    public async Task MintVoucherCode_OwnPendingVoucher_ReturnsVerifiableCode()
    {
        var detail = SeedDetail(_memberId, UsageStatus.Pending, "VC-M-00000002", "secret-2");
        var controller = CreateController(_memberId);

        var result = await controller.MintVoucherCode(_memberId, detail.Id, CancellationToken.None);

        var json = System.Text.Json.JsonSerializer.Serialize(
            result.Should().BeOfType<OkObjectResult>().Subject.Value);
        var token = System.Text.Json.JsonDocument.Parse(json).RootElement
            .GetProperty("code").GetString()!;

        // The minted token must pass the real verification with the detail's secret.
        var service = new VoucherCodeService();
        service.CheckCode(token, detail.VoucherCodeSecret, out var vid).Should().Be(VoucherCodeCheck.Valid);
        vid.Should().Be(detail.Id);
    }

    [Fact]
    public async Task MintVoucherCode_UsedVoucher_ReturnsConflictWithCauseAndNextAction()
    {
        var detail = SeedDetail(_memberId, UsageStatus.Complete, "VC-M-00000003", "secret-3");
        var controller = CreateController(_memberId);

        var result = await controller.MintVoucherCode(_memberId, detail.Id, CancellationToken.None);

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflict.Value.ToString().Should().Contain("VoucherNotUsable");
        conflict.Value.ToString().Should().Contain("check the voucher status in your wallet");
    }

    [Fact]
    public async Task MintVoucherCode_VoucherOwnedByAnotherMember_ReturnsNotFound()
    {
        var detail = SeedDetail(_otherMemberId, UsageStatus.Pending, "VC-M-00000004", "secret-4");
        var controller = CreateController(_memberId);

        var result = await controller.MintVoucherCode(_memberId, detail.Id, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task MintVoucherCode_CrossMemberPath_ReturnsForbidden()
    {
        var detail = SeedDetail(_memberId, UsageStatus.Pending, "VC-M-00000005", "secret-5");
        var controller = CreateController(_memberId);

        var result = await controller.MintVoucherCode(_otherMemberId, detail.Id, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task MintVoucherCode_UnknownVoucherId_ReturnsNotFound()
    {
        var controller = CreateController(_memberId);

        var result = await controller.MintVoucherCode(_memberId, Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task MintVoucherCode_MissingIdentity_ReturnsUnauthorized()
    {
        var controller = CreateController(null);

        var result = await controller.MintVoucherCode(_memberId, Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class StubPlanRepository : Repository<VoucherPlanHeader>, IVoucherPlanRepository
    {
        public StubPlanRepository(ApplicationDbContext context) : base(context) { }

        public Task<IReadOnlyList<VoucherPlanHeader>> ListByBrandAsync(Guid brandId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VoucherPlanHeader>>(Array.Empty<VoucherPlanHeader>());

        public Task<IReadOnlyList<VoucherPlanHeader>> ListByBrandAndStatusAsync(Guid brandId, ApprovalStatus status, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VoucherPlanHeader>>(Array.Empty<VoucherPlanHeader>());

        public Task<VoucherPlanHeader?> GetByIdWithOutletsAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<VoucherPlanHeader?>(null);
    }

    private sealed class CountingVoucherCodeService : IVoucherCodeService
    {
        public int GenerateCodeCalls { get; private set; }

        public string GenerateCode(Guid voucherDetailId, string secretKey, int? validitySeconds = null)
        {
            GenerateCodeCalls++;
            return "unused";
        }

        public VoucherCodeCheck CheckCode(string code, string secretKey, out Guid voucherId)
        {
            voucherId = Guid.Empty;
            return VoucherCodeCheck.Malformed;
        }

        public string GenerateSecretKey() => Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        public bool TryExtractVoucherId(string code, out Guid voucherId)
        {
            voucherId = Guid.Empty;
            return false;
        }
    }
}
