using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NonCash.Core.Configuration;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;
using NonCash.Infrastructure.Data;
using NonCash.Infrastructure.Repositories;
using NonCash.Infrastructure.Services;

namespace NonCash.IntegrationTests.CustomerActions;

/// <summary>
/// Customer-action-matrix enforcement through the real service layer on SQLite:
/// per-brand block (S1) vs. platform blacklist (S2) across promotion, purchase,
/// and transfer (docs/customer-action-matrix.md rows 1, 3-4, 8).
/// </summary>
public class BrandCustomerEnforcementTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _context;
    private readonly PromotionService _promotionService;
    private readonly PurchaseService _purchaseService;
    private readonly TransferService _transferService;

    private Guid BusinessId { get; } = Guid.Parse("15000000-0000-0000-0000-000000000001");
    private Guid BrandAId { get; } = Guid.Parse("16000000-0000-0000-0000-000000000001");
    private Guid BrandBId { get; } = Guid.Parse("16000000-0000-0000-0000-000000000002");
    private Guid StaffUserId { get; } = Guid.Parse("17000000-0000-0000-0000-000000000001");
    private Guid PromoPlanId { get; } = Guid.Parse("18000000-0000-0000-0000-000000000001");
    private Guid GiftPlanAId { get; } = Guid.Parse("18000000-0000-0000-0000-000000000002");
    private Guid GiftPlanBId { get; } = Guid.Parse("18000000-0000-0000-0000-000000000003");
    private Guid CarolCustomerId { get; } = Guid.Parse("19000000-0000-0000-0000-000000000001");
    private Guid DaveCustomerId { get; } = Guid.Parse("19000000-0000-0000-0000-000000000002");
    private Guid EveCustomerId { get; } = Guid.Parse("19000000-0000-0000-0000-000000000003");
    private Guid CarolMemberId { get; } = Guid.Parse("1a000000-0000-0000-0000-000000000001");
    private Guid EveMemberId { get; } = Guid.Parse("1a000000-0000-0000-0000-000000000002");
    private Guid EveVoucherId { get; } = Guid.Parse("1b000000-0000-0000-0000-000000000001");

    private const string CarolPhone = "0909333333";
    private const string DavePhone = "0909444444";
    private const string EvePhone = "0909555555";

    public BrandCustomerEnforcementTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        var notificationService = new ConsoleNotificationService();
        var brandCustomerRepository = new BrandCustomerRepository(_context);

        _promotionService = new PromotionService(
            new VoucherPlanRepository(_context),
            new Repository<VoucherPlanDetail>(_context),
            new CustomerRepository(_context),
            new MemberAccountRepository(_context),
            new Repository<VoucherDistribution>(_context),
            new Repository<VoucherUsage>(_context),
            new VoucherTransferRepository(_context),
            new Repository<Outlet>(_context),
            notificationService,
            brandCustomerRepository,
            new Repository<VoucherDistributionBatch>(_context));

        _purchaseService = new PurchaseService(
            new VoucherPlanRepository(_context),
            new Repository<VoucherPlanDetail>(_context),
            new Repository<PurchaseOrder>(_context),
            new Repository<OrderDetail>(_context),
            new Repository<VoucherDistribution>(_context),
            new MemberAccountRepository(_context),
            new CustomerRepository(_context),
            brandCustomerRepository);

        _transferService = new TransferService(
            new Repository<VoucherPlanDetail>(_context),
            new CustomerRepository(_context),
            new MemberAccountRepository(_context),
            new Repository<VoucherDistribution>(_context),
            notificationService,
            new VoucherEventPublisher(_context),
            brandCustomerRepository,
            new VoucherPlanRepository(_context));

        Seed();
    }

    private void Seed()
    {
        _context.Businesses.Add(new Business
        {
            Id = BusinessId,
            BusinessName = "Enforcement Business",
            TaxCode = "TAX-ENF",
            Address = "Test Address",
            IsActive = true
        });

        _context.Brands.AddRange(
            new Brand { Id = BrandAId, BusinessId = BusinessId, Name = "Brand A", TaxCode = "TAX-ENF-A", Status = BrandStatus.Active },
            new Brand { Id = BrandBId, BusinessId = BusinessId, Name = "Brand B", TaxCode = "TAX-ENF-B", Status = BrandStatus.Active });

        _context.UserAccounts.Add(new UserAccount
        {
            Id = StaffUserId,
            BrandId = BrandAId,
            Username = "enfstaff",
            PasswordHash = "not-a-real-hash",
            FullName = "Enforcement Staff",
            Role = UserRole.BrandManager,
            Status = UserStatus.Active
        });

        // Carol: active but blocked at Brand A only (per-brand block, S1).
        // Dave: active and unknown to both brands (clean).
        // Eve: platform-blacklisted (S2).
        _context.Customers.AddRange(
            new Customer { Id = CarolCustomerId, PhoneNumber = CarolPhone, FullName = "Carol Blocked At A", Email = "carol@example.com", Status = CustomerStatus.Active },
            new Customer { Id = DaveCustomerId, PhoneNumber = DavePhone, FullName = "Dave Clean", Email = "dave@example.com", Status = CustomerStatus.Active },
            new Customer { Id = EveCustomerId, PhoneNumber = EvePhone, FullName = "Eve Blacklisted", Email = "eve@example.com", Status = CustomerStatus.Blacklisted });

        _context.MemberAccounts.AddRange(
            new MemberAccount { Id = CarolMemberId, CustomerId = CarolCustomerId, Username = "carol", PasswordHash = "x", FullName = "Carol Blocked At A", Status = MemberAccountStatus.Active },
            new MemberAccount { Id = EveMemberId, CustomerId = EveCustomerId, Username = "eve", PasswordHash = "x", FullName = "Eve Blacklisted", Status = MemberAccountStatus.Active });

        // Carol's Brand A relationship, blocked at this brand.
        _context.BrandCustomers.Add(new BrandCustomer
        {
            BrandId = BrandAId,
            CustomerId = CarolCustomerId,
            Source = BrandCustomerSource.Import,
            IsBlocked = true,
            BlockedAt = DateTime.UtcNow
        });

        // Incidental credit balances for both brands. Purchase/promotion are no longer
        // credit-gated (charging happens at plan approval), so these are not asserted on.
        _context.CreditBatches.AddRange(
            SeedBatch(BrandAId),
            SeedBatch(BrandBId));

        _context.VoucherPlanHeaders.AddRange(
            SeedPlan(PromoPlanId, BrandAId, VoucherType.Complimentary),
            SeedPlan(GiftPlanAId, BrandAId, VoucherType.Gift),
            SeedPlan(GiftPlanBId, BrandBId, VoucherType.Gift));

        // Stock: two unassigned vouchers under the promo plan and one under Brand B's
        // gift plan (the allowed purchase). Gift plan A needs none — Carol is rejected
        // before the stock check. Eve holds one promo voucher as transfer stock.
        _context.VoucherPlanDetails.AddRange(
            new VoucherPlanDetail { Id = Guid.NewGuid(), ParentId = PromoPlanId, SerialNo = "ENF-0001", VoucherCodeSecret = "s1", UsageStatus = UsageStatus.Pending },
            new VoucherPlanDetail { Id = Guid.NewGuid(), ParentId = PromoPlanId, SerialNo = "ENF-0002", VoucherCodeSecret = "s2", UsageStatus = UsageStatus.Pending },
            new VoucherPlanDetail { Id = Guid.NewGuid(), ParentId = GiftPlanBId, SerialNo = "ENF-0003", VoucherCodeSecret = "s3", UsageStatus = UsageStatus.Pending },
            new VoucherPlanDetail { Id = EveVoucherId, ParentId = PromoPlanId, SerialNo = "ENF-0004", VoucherCodeSecret = "s4", MemberId = EveMemberId, UsageStatus = UsageStatus.Pending });

        _context.SaveChanges();
    }

    private static CreditBatch SeedBatch(Guid brandId) => new()
    {
        Id = Guid.NewGuid(),
        BrandId = brandId,
        BatchType = CreditBatchType.Purchase,
        OriginalAmount = 100,
        RemainingAmount = 100,
        PricePerCreditVnd = 0m,
        TotalPaidVnd = 0m,
        ExpiresAt = DateTime.UtcNow.AddYears(1),
        CreatedAt = DateTime.UtcNow.AddDays(-1)
    };

    private VoucherPlanHeader SeedPlan(Guid id, Guid brandId, VoucherType type) => new()
    {
        Id = id,
        PlanDate = DateTime.UtcNow,
        CreatorId = StaffUserId,
        BrandId = brandId,
        VoucherType = type,
        ValueType = VoucherValueType.Value,
        FaceValue = 100000m,
        NetValue = 100000m,
        ExpiryDate = DateTime.UtcNow.AddYears(1),
        PublishDate = DateTime.UtcNow.AddDays(-1),
        ValidFrom = DateTime.UtcNow,
        ValidTo = DateTime.UtcNow.AddYears(1),
        TargetQuantity = 10,
        Budget = 1000000m,
        ApprovalStatus = ApprovalStatus.Approved,
        VersionNumber = 1
    };

    // ----- Matrix row 1 (S1): promotion distribution -----

    [Fact]
    public async Task DistributeAsync_BrandBlockedRecipient_Skipped()
    {
        // Act
        var result = await _promotionService.DistributeAsync(
            PromoPlanId,
            BrandAId,
            new List<string> { CarolPhone, DavePhone },
            NotificationChannel.None);

        // Assert — Carol is skipped with the BrandBlocked reason, Dave gets the voucher
        result.Success.Should().BeTrue();
        result.DistributedCount.Should().Be(1);
        result.SkippedCount.Should().Be(1);
        result.SkippedRecords.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new SkippedRecord(CarolPhone, "BrandBlocked"));

        // Carol is skipped before the auto-link: her existing mapping stays blocked
        // and its source is never upgraded.
        var carolMapping = await _context.BrandCustomers
            .SingleAsync(bc => bc.BrandId == BrandAId && bc.CustomerId == CarolCustomerId);
        carolMapping.IsBlocked.Should().BeTrue();
        carolMapping.Source.Should().Be(BrandCustomerSource.Import);

        // Dave is auto-linked to the promoting brand and holds the distributed voucher.
        var daveMapping = await _context.BrandCustomers
            .SingleAsync(bc => bc.BrandId == BrandAId && bc.CustomerId == DaveCustomerId);
        daveMapping.Source.Should().Be(BrandCustomerSource.PromotionAuto);
        daveMapping.IsBlocked.Should().BeFalse();

        var daveMember = await _context.MemberAccounts.SingleAsync(m => m.CustomerId == DaveCustomerId);
        (await _context.VoucherPlanDetails.CountAsync(d =>
            d.ParentId == PromoPlanId && d.MemberId == daveMember.Id)).Should().Be(1);
    }

    // ----- Matrix rows 3-4 (S1): purchase scope -----

    [Fact]
    public async Task CreateOrder_BrandBlockedCustomer_RejectedForOwnBrandPlans_AllowedForOtherBrand()
    {
        // Act — Carol is blocked at Brand A only: Brand A's plan is rejected,
        // Brand B's plan goes through.
        var blockedOrder = await _purchaseService.CreateOrderAsync(
            new CreateOrderInput(CarolMemberId, GiftPlanAId, 1, null, null));

        // Assert — rejected for the blocking brand's own plan
        blockedOrder.Success.Should().BeFalse();
        blockedOrder.ErrorCode.Should().Be("BrandBlocked");
        blockedOrder.Order.Should().BeNull();

        // Act — the same customer buys Brand B's plan
        var allowedOrder = await _purchaseService.CreateOrderAsync(
            new CreateOrderInput(CarolMemberId, GiftPlanBId, 1, null, null));

        // Assert — allowed for another brand
        allowedOrder.Success.Should().BeTrue();

        // Confirming the allowed order auto-links Carol to Brand B (SelfPurchase).
        var confirmed = await _purchaseService.ConfirmPaymentAsync(allowedOrder.Order!.Id);
        confirmed.Success.Should().BeTrue();
        confirmed.AllocatedCount.Should().Be(1);

        var brandBMapping = await _context.BrandCustomers
            .SingleAsync(bc => bc.BrandId == BrandBId && bc.CustomerId == CarolCustomerId);
        brandBMapping.Source.Should().Be(BrandCustomerSource.SelfPurchase);
        brandBMapping.IsBlocked.Should().BeFalse();
    }

    // ----- Matrix row 8 (S2 + gap #3): transfer out -----

    [Fact]
    public async Task Transfer_SenderBlacklisted_Rejected()
    {
        // Act — Eve (platform-blacklisted) tries to transfer her voucher to Carol
        var result = await _transferService.TransferAsync(
            EveMemberId,
            new List<Guid> { EveVoucherId },
            new List<string> { CarolPhone });

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("SenderBlacklisted");

        // The voucher stays with the sender — nothing moved.
        var voucher = await _context.VoucherPlanDetails.FindAsync(EveVoucherId);
        voucher!.MemberId.Should().Be(EveMemberId);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
