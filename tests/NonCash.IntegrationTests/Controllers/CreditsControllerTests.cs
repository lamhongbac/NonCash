using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NonCash.API.Controllers;
using NonCash.API.DTOs;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;
using NonCash.Infrastructure.Data;
using NonCash.Infrastructure.Repositories;
using NonCash.Infrastructure.Services;

namespace NonCash.IntegrationTests.Controllers;

/// <summary>
/// Epic 9 — prepaid credit billing. Uses SQLite in-memory (relational) so that
/// the POS lock/commit flow (ExecuteUpdate) and unique index enforcement work.
/// </summary>
public class CreditsControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _context;
    private readonly CreditService _creditService;

    private readonly Guid _brandAId = Guid.NewGuid();
    private readonly Guid _brandBId = Guid.NewGuid();
    private readonly Guid _staffUserId = Guid.NewGuid();
    private readonly Guid _outletId = Guid.NewGuid();
    private readonly Guid _memberId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    public CreditsControllerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();
        _creditService = new CreditService(_context, NullLogger<CreditService>.Instance);

        Seed();
    }

    private void Seed()
    {
        var business = new Business
        {
            BusinessName = "Credit Test Business",
            TaxCode = "CRED-BUS",
            Address = "Test Address",
            IsActive = true
        };
        _context.Businesses.Add(business);
        _context.SaveChanges();

        _context.Brands.AddRange(
            new Brand { Id = _brandAId, BusinessId = business.Id, Name = "Brand A", TaxCode = "CRED-A", Status = BrandStatus.Active },
            new Brand { Id = _brandBId, BusinessId = business.Id, Name = "Brand B", TaxCode = "CRED-B", Status = BrandStatus.Active });

        _context.UserAccounts.Add(new UserAccount
        {
            Id = _staffUserId,
            BrandId = _brandAId,
            Username = "credit-staff",
            PasswordHash = "hash",
            FullName = "Credit Staff",
            Role = UserRole.BrandManager,
            Status = UserStatus.Active
        });

        _context.Outlets.Add(new Outlet
        {
            Id = _outletId,
            BrandId = _brandAId,
            Name = "Outlet A1",
            Status = OutletStatus.Active
        });

        _context.Customers.Add(new Customer
        {
            Id = _customerId,
            PhoneNumber = "0909333333",
            FullName = "Credit Member",
            Status = CustomerStatus.Active
        });

        _context.MemberAccounts.Add(new MemberAccount
        {
            Id = _memberId,
            CustomerId = _customerId,
            Username = "creditmember",
            PasswordHash = "hash",
            FullName = "Credit Member",
            Status = MemberAccountStatus.Active
        });

        _context.SaveChanges();
    }

    private CreditsController CreateController(string role, Guid? brandId)
    {
        return new CreditsController(_creditService, new FakeCurrentUserService(role, brandId));
    }

    private VoucherPlanHeader SeedPlan(Guid brandId, VoucherType voucherType, int voucherCount, bool memberOwned = false)
    {
        var plan = new VoucherPlanHeader
        {
            PlanDate = DateTime.UtcNow,
            CreatorId = _staffUserId,
            BrandId = brandId,
            VoucherType = voucherType,
            ValueType = VoucherValueType.Value,
            FaceValue = 50000m,
            NetValue = 45000m,
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            PublishDate = DateTime.UtcNow.AddDays(-1),
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            ValidTo = DateTime.UtcNow.AddYears(1),
            TargetQuantity = voucherCount,
            Budget = 500000m,
            ApprovalStatus = ApprovalStatus.Approved,
            VersionNumber = 1
        };
        _context.VoucherPlanHeaders.Add(plan);
        _context.SaveChanges();

        _context.PlanOutlets.Add(new PlanOutlet { PlanId = plan.Id, OutletId = _outletId });

        for (var i = 1; i <= voucherCount; i++)
        {
            _context.VoucherPlanDetails.Add(new VoucherPlanDetail
            {
                ParentId = plan.Id,
                SerialNo = $"VC-CRED-{voucherType}-{i:D8}",
                VoucherCodeSecret = $"secret-{plan.Id}-{i}",
                MemberId = memberOwned ? _memberId : null,
                UsageStatus = UsageStatus.Pending
            });
        }
        _context.SaveChanges();

        return plan;
    }

    // ----- balance & ledger scoping -----

    [Fact]
    public async Task GetBalance_BrandUser_ReturnsOwnBrandBalance()
    {
        await _creditService.TopUpAsync(_brandAId, 500, CreditEntryType.Grant, "welcome", null);
        var controller = CreateController("BrandManager", _brandAId);

        var result = await controller.GetBalance(null, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<CreditBalanceResponse>().Subject;
        response.BrandId.Should().Be(_brandAId);
        response.Balance.Should().Be(500);
    }

    [Fact]
    public async Task GetBalance_BrandUser_RequestingOtherBrand_IsForbidden()
    {
        var controller = CreateController("BrandManager", _brandAId);

        var result = await controller.GetBalance(_brandBId, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetBalance_Admin_CanQueryAnyBrand()
    {
        await _creditService.TopUpAsync(_brandBId, 42, CreditEntryType.Purchase, null, null);
        var controller = CreateController("Admin", null);

        var result = await controller.GetBalance(_brandBId, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<CreditBalanceResponse>().Subject;
        response.BrandId.Should().Be(_brandBId);
        response.Balance.Should().Be(42);
    }

    [Fact]
    public async Task GetLedger_BrandUser_SeesOwnEntriesOnly()
    {
        await _creditService.TopUpAsync(_brandAId, 500, CreditEntryType.Grant, null, null);
        await _creditService.TopUpAsync(_brandBId, 999, CreditEntryType.Grant, null, null);
        var controller = CreateController("BrandManager", _brandAId);

        var result = await controller.GetLedger(null, null, null, null, 1, 50, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<CreditLedgerResponse>().Subject;
        response.Entries.Should().OnlyContain(e => e.BrandId == _brandAId);
        response.TotalCount.Should().Be(1);
    }

    // ----- top-up -----

    [Fact]
    public void TopUp_IsRestrictedToAdminRole()
    {
        var attribute = typeof(CreditsController)
            .GetMethod(nameof(CreditsController.TopUp))!
            .GetCustomAttribute<AuthorizeAttribute>();

        attribute.Should().NotBeNull();
        attribute!.Roles.Should().Be("Admin");
    }

    [Fact]
    public async Task TopUp_Admin_CreatesLedgerEntry()
    {
        var controller = CreateController("Admin", null);
        var request = new CreditTopUpRequest(_brandAId, 300, "Purchase", "bank transfer #7");

        var result = await controller.TopUp(request, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var entry = ok.Value.Should().BeOfType<CreditLedgerEntryDto>().Subject;
        entry.EntryType.Should().Be("Purchase");
        entry.Amount.Should().Be(300);
        (await _creditService.GetBalanceAsync(_brandAId)).Should().Be(300);
    }

    [Fact]
    public async Task TopUp_WithConsumptionType_ReturnsBadRequest()
    {
        var controller = CreateController("Admin", null);
        var request = new CreditTopUpRequest(_brandAId, -1, "Consumption", null);

        var result = await controller.TopUp(request, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TopUp_WithNegativePurchase_ReturnsBadRequest()
    {
        var controller = CreateController("Admin", null);
        var request = new CreditTopUpRequest(_brandAId, -10, "Purchase", null);

        var result = await controller.TopUp(request, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ----- end-to-end value moments -----

    private PurchaseService CreatePurchaseService() => new(
        new VoucherPlanRepository(_context),
        new Repository<VoucherPlanDetail>(_context),
        new Repository<PurchaseOrder>(_context),
        new Repository<OrderDetail>(_context),
        new Repository<VoucherDistribution>(_context),
        new MemberAccountRepository(_context),
        new CustomerRepository(_context),
        _creditService);

    private PosService CreatePosService() => new(
        new Repository<VoucherPlanDetail>(_context),
        new VoucherPlanRepository(_context),
        new Repository<Outlet>(_context),
        new BrandRepository(_context),
        new VoucherCodeService(),
        new VoucherLockRepository(_context),
        new SettlementService(_context),
        _creditService);

    [Fact]
    public async Task ConfirmGiftPayment_ChargesOneCreditPerVoucher()
    {
        await _creditService.TopUpAsync(_brandAId, 10, CreditEntryType.Grant, "welcome", null);
        var plan = SeedPlan(_brandAId, VoucherType.Gift, voucherCount: 3);
        var purchaseService = CreatePurchaseService();

        var orderResult = await purchaseService.CreateOrderAsync(
            new CreateOrderInput(_memberId, plan.Id, 2, null, null));
        orderResult.Success.Should().BeTrue();

        var payResult = await purchaseService.ConfirmPaymentAsync(orderResult.Order!.Id);

        payResult.Success.Should().BeTrue();
        payResult.AllocatedCount.Should().Be(2);
        var consumptions = await _context.CreditLedgerEntries
            .Where(c => c.BrandId == _brandAId && c.EntryType == CreditEntryType.Consumption)
            .ToListAsync();
        consumptions.Should().HaveCount(2);
        consumptions.Should().OnlyContain(c => c.Amount == -1 && c.VoucherDetailId != null);
        (await _creditService.GetBalanceAsync(_brandAId)).Should().Be(8);
    }

    [Fact]
    public async Task ConfirmGiftPayment_ReplayedConfirm_DoesNotDoubleCharge()
    {
        await _creditService.TopUpAsync(_brandAId, 10, CreditEntryType.Grant, null, null);
        var plan = SeedPlan(_brandAId, VoucherType.Gift, voucherCount: 2);
        var purchaseService = CreatePurchaseService();
        var orderResult = await purchaseService.CreateOrderAsync(
            new CreateOrderInput(_memberId, plan.Id, 1, null, null));

        await purchaseService.ConfirmPaymentAsync(orderResult.Order!.Id);
        await purchaseService.ConfirmPaymentAsync(orderResult.Order!.Id); // idempotent replay

        var consumptionCount = await _context.CreditLedgerEntries
            .CountAsync(c => c.EntryType == CreditEntryType.Consumption);
        consumptionCount.Should().Be(1);
    }

    [Fact]
    public async Task PosCommit_Complimentary_ChargesOneCreditToIssuingBrand()
    {
        await _creditService.TopUpAsync(_brandAId, 5, CreditEntryType.Grant, null, null);
        var plan = SeedPlan(_brandAId, VoucherType.Complimentary, voucherCount: 1, memberOwned: true);
        var detail = await _context.VoucherPlanDetails.SingleAsync(d => d.ParentId == plan.Id);
        var code = new VoucherCodeService().GenerateCode(detail.Id, detail.VoucherCodeSecret);
        var posService = CreatePosService();

        // ExecuteUpdate bypasses the change tracker; drop tracked seed entities so the
        // shared test context re-reads current values (production uses scoped contexts).
        _context.ChangeTracker.Clear();

        var lockResult = await posService.LockAsync(code, _outletId, "BILL-001");
        lockResult.Reason.Should().BeNull();
        lockResult.Status.Should().Be("Locked");

        // Lock and Commit are separate requests (separate scoped contexts) in production.
        _context.ChangeTracker.Clear();

        var commitResult = await posService.CommitAsync(lockResult.LockId!.Value, "TXN-COMP-1", 50000m, _outletId);

        commitResult.Status.Should().Be("Success");
        var consumptions = await _context.CreditLedgerEntries
            .Where(c => c.EntryType == CreditEntryType.Consumption)
            .ToListAsync();
        consumptions.Should().ContainSingle();
        consumptions[0].BrandId.Should().Be(_brandAId);
        consumptions[0].VoucherDetailId.Should().Be(detail.Id);
        (await _creditService.GetBalanceAsync(_brandAId)).Should().Be(4);
    }

    [Fact]
    public async Task PosCommit_Gift_CreatesNoNewConsumption()
    {
        await _creditService.TopUpAsync(_brandAId, 5, CreditEntryType.Grant, null, null);
        var plan = SeedPlan(_brandAId, VoucherType.Gift, voucherCount: 1, memberOwned: true);
        var detail = await _context.VoucherPlanDetails.SingleAsync(d => d.ParentId == plan.Id);
        var code = new VoucherCodeService().GenerateCode(detail.Id, detail.VoucherCodeSecret);
        var posService = CreatePosService();

        _context.ChangeTracker.Clear();

        var lockResult = await posService.LockAsync(code, _outletId, "BILL-002");
        lockResult.Reason.Should().BeNull();
        lockResult.Status.Should().Be("Locked");

        _context.ChangeTracker.Clear();

        var commitResult = await posService.CommitAsync(lockResult.LockId!.Value, "TXN-GIFT-1", 50000m, _outletId);

        // Gift was charged at sale, never at redemption.
        commitResult.Status.Should().Be("Success");
        var consumptionCount = await _context.CreditLedgerEntries
            .CountAsync(c => c.EntryType == CreditEntryType.Consumption);
        consumptionCount.Should().Be(0);
    }

    // ----- guards -----

    [Fact]
    public async Task GenerateBatch_BlockedAtZeroBalance()
    {
        var plan = SeedPlan(_brandAId, VoucherType.Gift, voucherCount: 0);
        var generationService = new VoucherGenerationService(
            new VoucherPlanRepository(_context),
            new VoucherCodeService(),
            new Repository<VoucherPlanDetail>(_context),
            _creditService);

        var result = await generationService.GenerateBatchAsync(plan.Id, 10, _brandAId);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("InsufficientCredits");
    }

    [Fact]
    public async Task CreateOrder_BlockedAtZeroBalance()
    {
        var plan = SeedPlan(_brandAId, VoucherType.Gift, voucherCount: 2);
        var purchaseService = CreatePurchaseService();

        var result = await purchaseService.CreateOrderAsync(
            new CreateOrderInput(_memberId, plan.Id, 1, null, null));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("InsufficientCredits");
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private class FakeCurrentUserService : ICurrentUserService
    {
        private readonly string _role;
        private readonly Guid? _brandId;

        public FakeCurrentUserService(string role, Guid? brandId)
        {
            _role = role;
            _brandId = brandId;
        }

        public Guid? GetCurrentBrandId() => _brandId;
        public string? GetCurrentUserId() => Guid.NewGuid().ToString();
        public string? GetCurrentUserRole() => _role;
        public bool IsInRole(string role) => role == _role;
        public Guid? GetCurrentCustomerId() => null;
    }
}
