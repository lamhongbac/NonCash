using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NonCash.API.Controllers;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;
using NonCash.Infrastructure.Data;
using NonCash.Infrastructure.Repositories;
using NonCash.Infrastructure.Services;
using NonCash.IntegrationTests.Fixtures;

namespace NonCash.IntegrationTests.Controllers;

/// <summary>
/// Batch distribution traceability: every promotion run persists a VoucherDistributionBatch
/// row and links its distributions to it; the runs, the plan voucher ledger and the customer
/// voucher history are queryable brand-scoped. Single-voucher flows (sale) create no batch.
/// </summary>
public class DistributionBatchTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _context;
    private readonly PromotionService _promotionService;
    private readonly DistributionBatchService _batchService;

    private static readonly Guid BusinessId = Guid.Parse("25000000-0000-0000-0000-000000000001");
    private static readonly Guid BrandAId = Guid.Parse("26000000-0000-0000-0000-000000000001");
    private static readonly Guid BrandBId = Guid.Parse("26000000-0000-0000-0000-000000000002");
    private static readonly Guid StaffUserId = Guid.Parse("27000000-0000-0000-0000-000000000001");
    private static readonly Guid PromoPlanId = Guid.Parse("28000000-0000-0000-0000-000000000001");
    private static readonly Guid ExpiredPlanId = Guid.Parse("28000000-0000-0000-0000-000000000002");
    private static readonly Guid OtherBrandPlanId = Guid.Parse("28000000-0000-0000-0000-000000000003");
    private static readonly Guid DaveCustomerId = Guid.Parse("29000000-0000-0000-0000-000000000001");
    private static readonly Guid CarolCustomerId = Guid.Parse("29000000-0000-0000-0000-000000000002");
    private static readonly Guid CarolMemberId = Guid.Parse("2a000000-0000-0000-0000-000000000001");

    private const string DavePhone = "0909444444";
    private const string CarolPhone = "0909333333";

    public DistributionBatchTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        var planRepository = new VoucherPlanRepository(_context);
        var detailRepository = new Repository<VoucherPlanDetail>(_context);
        var distributionRepository = new Repository<VoucherDistribution>(_context);
        var batchRepository = new Repository<VoucherDistributionBatch>(_context);
        var customerRepository = new CustomerRepository(_context);
        var memberRepository = new MemberAccountRepository(_context);
        var brandCustomerRepository = new BrandCustomerRepository(_context);

        _promotionService = new PromotionService(
            planRepository,
            detailRepository,
            customerRepository,
            memberRepository,
            distributionRepository,
            new Repository<VoucherUsage>(_context),
            new VoucherTransferRepository(_context),
            new Repository<Outlet>(_context),
            new FileNotificationService(),
            brandCustomerRepository,
            batchRepository,
            new StubJwtTokenService(),
            new ConfigurationBuilder().AddInMemoryCollection().Build());

        _batchService = new DistributionBatchService(
            planRepository,
            batchRepository,
            distributionRepository,
            detailRepository,
            memberRepository,
            customerRepository,
            new UserAccountRepository(_context),
            brandCustomerRepository,
            new BrandRepository(_context),
            new OutletRepository(_context));

        Seed();
    }

    private void Seed()
    {
        _context.Businesses.Add(new Business
        {
            Id = BusinessId,
            BusinessName = "Batch Business",
            TaxCode = "TAX-BAT",
            Address = "Test Address",
            IsActive = true
        });

        _context.Brands.AddRange(
            new Brand { Id = BrandAId, BusinessId = BusinessId, Name = "Brand A", TaxCode = "TAX-BAT-A", Status = BrandStatus.Active },
            new Brand { Id = BrandBId, BusinessId = BusinessId, Name = "Brand B", TaxCode = "TAX-BAT-B", Status = BrandStatus.Active });

        _context.UserAccounts.Add(new UserAccount
        {
            Id = StaffUserId,
            BrandId = BrandAId,
            Username = "batchstaff",
            PasswordHash = "not-a-real-hash",
            FullName = "Batch Staff",
            Role = UserRole.BrandManager,
            Status = UserStatus.Active
        });

        // Dave: clean recipient. Carol: blocked at Brand A -> skipped by the promotion run.
        _context.Customers.AddRange(
            new Customer { Id = DaveCustomerId, PhoneNumber = DavePhone, FullName = "Dave Clean", Email = "dave@example.com", Status = CustomerStatus.Active },
            new Customer { Id = CarolCustomerId, PhoneNumber = CarolPhone, FullName = "Carol Blocked", Email = "carol@example.com", Status = CustomerStatus.Active });

        _context.MemberAccounts.Add(
            new MemberAccount { Id = CarolMemberId, CustomerId = CarolCustomerId, PasswordHash = "x", FullName = "Carol Blocked", Status = MemberAccountStatus.Active });

        _context.BrandCustomers.Add(new BrandCustomer
        {
            BrandId = BrandAId,
            CustomerId = CarolCustomerId,
            Source = BrandCustomerSource.Import,
            IsBlocked = true,
            BlockedAt = DateTime.UtcNow
        });

        _context.VoucherPlanHeaders.AddRange(
            SeedPlan(PromoPlanId, BrandAId, DateTime.UtcNow.AddYears(1), "Promo Plan"),
            SeedPlan(ExpiredPlanId, BrandAId, DateTime.UtcNow.AddDays(-1), "Expired Plan"),
            SeedPlan(OtherBrandPlanId, BrandBId, DateTime.UtcNow.AddYears(1), "Other Brand Plan"));

        // Promo plan stock: 3 unassigned + 1 already redeemed by Carol's member.
        _context.VoucherPlanDetails.AddRange(
            StockVoucher(PromoPlanId, "BAT-0001"),
            StockVoucher(PromoPlanId, "BAT-0002"),
            StockVoucher(PromoPlanId, "BAT-0003"),
            new VoucherPlanDetail
            {
                Id = Guid.NewGuid(),
                ParentId = PromoPlanId,
                SerialNo = "BAT-REDEEMED",
                VoucherCodeSecret = "sr",
                MemberId = CarolMemberId,
                UsageStatus = UsageStatus.Complete,
                UsedDate = DateTime.UtcNow.AddDays(-2)
            });

        _context.SaveChanges();
    }

    private static VoucherPlanDetail StockVoucher(Guid planId, string serial) => new()
    {
        Id = Guid.NewGuid(),
        ParentId = planId,
        SerialNo = serial,
        VoucherCodeSecret = "s-" + serial,
        UsageStatus = UsageStatus.Pending
    };

    private static VoucherPlanHeader SeedPlan(Guid id, Guid brandId, DateTime expiry, string displayName) => new()
    {
        Id = id,
        PlanDate = DateTime.UtcNow,
        CreatorId = StaffUserId,
        BrandId = brandId,
        VoucherType = VoucherType.Complimentary,
        ValueType = VoucherValueType.Value,
        FaceValue = 100000m,
        NetValue = 100000m,
        ExpiryDate = expiry,
        PublishDate = DateTime.UtcNow.AddDays(-10),
        ValidFrom = DateTime.UtcNow.AddDays(-10),
        ValidTo = expiry,
        TargetQuantity = 10,
        Budget = 1000000m,
        ApprovalStatus = ApprovalStatus.Approved,
        VersionNumber = 1,
        DisplayName = displayName
    };

    /// <summary>Runs the promotion for Dave (valid) + Carol (brand-blocked) as the staff user.</summary>
    private async Task<PromotionResult> RunPromotionAsync()
    {
        var result = await _promotionService.DistributeAsync(
            PromoPlanId,
            BrandAId,
            new List<string> { DavePhone, CarolPhone },
            NotificationChannel.None,
            createdById: StaffUserId);
        result.Success.Should().BeTrue();
        return result;
    }

    private Task<MemberAccount> GetDaveMemberAsync()
        => _context.MemberAccounts.SingleAsync(m => m.CustomerId == DaveCustomerId);

    // ----- Batch persistence (promotion) -----

    [Fact]
    public async Task DistributeAsync_PersistsBatchRow_AndLinksDistributions()
    {
        var result = await RunPromotionAsync();

        // The run returned its batch id and persisted exactly one batch row.
        result.BatchId.Should().NotBeNull();
        var batch = await _context.VoucherDistributionBatches.SingleAsync();
        batch.Id.Should().Be(result.BatchId!.Value);
        batch.PlanId.Should().Be(PromoPlanId);
        batch.BrandId.Should().Be(BrandAId);
        batch.CreatedById.Should().Be(StaffUserId);
        batch.NotifyChannel.Should().Be(NotificationChannel.None);
        batch.RecipientCount.Should().Be(1);
        batch.DistributedCount.Should().Be(1);
        batch.SkippedCount.Should().Be(1);
        batch.SkippedRecords.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new BatchSkippedRecipient { PhoneNumber = CarolPhone, Reason = "BrandBlocked" });

        // The delivered distribution links back to the batch.
        var distribution = await _context.VoucherDistributions.SingleAsync();
        distribution.BatchId.Should().Be(batch.Id);
        distribution.Method.Should().Be(DistributionMethod.Promotion);
    }

    // ----- Recipient rules: existence + duplicate -----

    [Fact]
    public async Task DistributeAsync_UnknownPhone_Skipped_AndCustomerNeverCreated()
    {
        var result = await _promotionService.DistributeAsync(
            PromoPlanId,
            BrandAId,
            new List<string> { DavePhone, "0909555555" },
            NotificationChannel.None);

        // Dave gets his voucher; the unknown phone is skipped, never auto-created.
        result.Success.Should().BeTrue();
        result.DistributedCount.Should().Be(1);
        result.SkippedCount.Should().Be(1);
        result.SkippedRecords.Should().ContainSingle()
            .Which.Should().Be(new SkippedRecord("0909555555", "CustomerNotFound"));

        // No customer record materialized for the unknown phone.
        (await _context.Customers.CountAsync()).Should().Be(2); // Dave + Carol only
        (await _context.Customers.AnyAsync(c => c.PhoneNumber == "0909555555")).Should().BeFalse();
        (await _context.VoucherDistributions.CountAsync()).Should().Be(1);

        // The skip is traceable on the persisted batch row.
        var batch = await _context.VoucherDistributionBatches.SingleAsync();
        batch.SkippedCount.Should().Be(1);
        batch.SkippedRecords.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new BatchSkippedRecipient { PhoneNumber = "0909555555", Reason = "CustomerNotFound" });
    }

    [Fact]
    public async Task DistributeAsync_DuplicateCustomer_SentOnce_AndReported()
    {
        // Dave appears three times: twice by phone, once by email.
        var result = await _promotionService.DistributeAsync(
            PromoPlanId,
            BrandAId,
            new List<string> { DavePhone, DavePhone, "dave@example.com" },
            NotificationChannel.None);

        // Exactly one voucher is sent; both later occurrences are reported as duplicates.
        result.Success.Should().BeTrue();
        result.DistributedCount.Should().Be(1);
        result.SkippedCount.Should().Be(2);
        result.SkippedRecords.Should().BeEquivalentTo(new[]
        {
            new SkippedRecord(DavePhone, "Duplicate"),
            new SkippedRecord("dave@example.com", "Duplicate")
        });

        var daveMember = await GetDaveMemberAsync();
        (await _context.VoucherPlanDetails.CountAsync(d => d.MemberId == daveMember.Id)).Should().Be(1);
        (await _context.VoucherDistributions.CountAsync()).Should().Be(1);

        var batch = await _context.VoucherDistributionBatches.SingleAsync();
        batch.RecipientCount.Should().Be(1);
        batch.DistributedCount.Should().Be(1);
        batch.SkippedCount.Should().Be(2);
        batch.SkippedRecords.Should().OnlyContain(s => s.Reason == "Duplicate");
    }

    [Fact]
    public async Task DistributeAsync_AllRecipientsUnknown_ReturnsNoEligibleCustomers_AndPersistsNothing()
    {
        var result = await _promotionService.DistributeAsync(
            PromoPlanId,
            BrandAId,
            new List<string> { "0909555555", "0909666666" },
            NotificationChannel.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("NoEligibleCustomers");
        result.SkippedRecords.Should().HaveCount(2).And.OnlyContain(s => s.Reason == "CustomerNotFound");

        // No batch, no distribution, no customer — the failed run leaves no trace behind.
        (await _context.VoucherDistributionBatches.CountAsync()).Should().Be(0);
        (await _context.VoucherDistributions.CountAsync()).Should().Be(0);
        (await _context.Customers.CountAsync()).Should().Be(2);
    }

    // ----- Batch list + detail queries (brand-scoped) -----

    [Fact]
    public async Task GetBatchesByPlanAsync_ReturnsRun_WithCreatorName_AndIsBrandScoped()
    {
        await RunPromotionAsync();

        var batches = await _batchService.GetBatchesByPlanAsync(PromoPlanId, BrandAId);

        var summary = batches.Should().ContainSingle().Which;
        summary.PlanName.Should().Be("Promo Plan");
        summary.CreatedByName.Should().Be("Batch Staff");
        summary.NotifyChannel.Should().Be("None");
        summary.RecipientCount.Should().Be(1);
        summary.DistributedCount.Should().Be(1);
        summary.SkippedCount.Should().Be(1);

        // Another brand never sees this plan's runs.
        (await _batchService.GetBatchesByPlanAsync(PromoPlanId, BrandBId)).Should().BeEmpty();
        (await _batchService.GetBatchesByPlanAsync(Guid.NewGuid(), BrandAId)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetBatchDetailAsync_ReturnsRecipientsAndSkipped_BrandScoped()
    {
        var result = await RunPromotionAsync();
        var daveMember = await GetDaveMemberAsync();
        var daveVoucher = await _context.VoucherPlanDetails.SingleAsync(d => d.MemberId == daveMember.Id);

        var detail = await _batchService.GetBatchDetailAsync(result.BatchId!.Value, BrandAId);

        detail.Should().NotBeNull();
        detail!.Summary.DistributedCount.Should().Be(1);

        var recipient = detail.Recipients.Should().ContainSingle().Which;
        recipient.VoucherId.Should().Be(daveVoucher.Id);
        recipient.SerialNo.Should().Be(daveVoucher.SerialNo);
        recipient.RecipientPhone.Should().Be(DavePhone);
        recipient.RecipientName.Should().Be("Dave Clean");
        recipient.Status.Should().Be("Distributed");
        recipient.Method.Should().Be("Promotion");

        detail.SkippedRecords.Should().ContainSingle()
            .Which.PhoneNumber.Should().Be(CarolPhone);

        // Cross-brand access looks like a 404 to the caller.
        (await _batchService.GetBatchDetailAsync(result.BatchId!.Value, BrandBId)).Should().BeNull();
    }

    // ----- Plan voucher ledger (recipient + lifecycle status) -----

    [Fact]
    public async Task GetPlanVoucherLedgerAsync_ShowsLifecycleRecipient_AndSaleWithoutBatch()
    {
        var result = await RunPromotionAsync();
        var daveMember = await GetDaveMemberAsync();

        // Simulate a single sale (no batch record): assign one remaining stock voucher directly.
        var saleVoucher = await _context.VoucherPlanDetails
            .FirstAsync(d => d.ParentId == PromoPlanId && d.MemberId == null);
        saleVoucher.MemberId = daveMember.Id;
        _context.VoucherDistributions.Add(new VoucherDistribution
        {
            Id = Guid.NewGuid(),
            VoucherId = saleVoucher.Id,
            MemberId = daveMember.Id,
            Method = DistributionMethod.Sale,
            DistributionDate = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var ledger = await _batchService.GetPlanVoucherLedgerAsync(PromoPlanId, BrandAId);

        ledger.Should().HaveCount(4);

        var promoRow = ledger.Single(r => r.BatchId == result.BatchId);
        promoRow.Status.Should().Be("Distributed");
        promoRow.RecipientPhone.Should().Be(DavePhone);
        promoRow.RecipientName.Should().Be("Dave Clean");
        promoRow.DistributionMethod.Should().Be("Promotion");
        promoRow.DistributedAt.Should().NotBeNull();

        var saleRow = ledger.Single(r => r.Id == saleVoucher.Id);
        saleRow.Status.Should().Be("Distributed");
        saleRow.DistributionMethod.Should().Be("Sale");
        saleRow.BatchId.Should().BeNull(); // single-voucher flows never create batch rows
        saleRow.RecipientPhone.Should().Be(DavePhone);

        var stockRow = ledger.Single(r => r.Status == "InStock");
        stockRow.RecipientPhone.Should().BeNull();
        stockRow.DistributionMethod.Should().BeNull();

        var redeemedRow = ledger.Single(r => r.SerialNo == "BAT-REDEEMED");
        redeemedRow.Status.Should().Be("Redeemed");
        redeemedRow.UsedDate.Should().NotBeNull();

        // Brand isolation on the ledger itself.
        (await _batchService.GetPlanVoucherLedgerAsync(PromoPlanId, BrandBId)).Should().BeEmpty();
    }

    // ----- Customer voucher history (brand-scoped) -----

    [Fact]
    public async Task GetCustomerVouchersAsync_ReturnsBrandScopedHistory_WithLifecycle()
    {
        await RunPromotionAsync();
        var daveMember = await GetDaveMemberAsync();

        // An expired-plan voucher and another brand's voucher, both held by Dave.
        _context.VoucherPlanDetails.AddRange(
            new VoucherPlanDetail
            {
                Id = Guid.NewGuid(),
                ParentId = ExpiredPlanId,
                SerialNo = "BAT-EXPIRED",
                VoucherCodeSecret = "se",
                MemberId = daveMember.Id,
                UsageStatus = UsageStatus.Pending
            },
            new VoucherPlanDetail
            {
                Id = Guid.NewGuid(),
                ParentId = OtherBrandPlanId,
                SerialNo = "BAT-OTHER",
                VoucherCodeSecret = "so",
                MemberId = daveMember.Id,
                UsageStatus = UsageStatus.Pending
            });
        await _context.SaveChangesAsync();

        // Dave was auto-linked to Brand A by the promotion run.
        var history = await _batchService.GetCustomerVouchersAsync(DaveCustomerId, BrandAId);

        history.Should().NotBeNull();
        history!.Should().HaveCount(2); // promo voucher + expired-plan voucher; other brand hidden
        history.Should().NotContain(r => r.SerialNo == "BAT-OTHER");

        var promoRow = history.Single(r => r.PlanId == PromoPlanId);
        promoRow.PlanName.Should().Be("Promo Plan");
        promoRow.Status.Should().Be("Distributed");
        promoRow.DistributionMethod.Should().Be("Promotion");

        var expiredRow = history.Single(r => r.PlanId == ExpiredPlanId);
        expiredRow.Status.Should().Be("Expired");
    }

    [Fact]
    public async Task GetCustomerVouchersAsync_UnmappedCustomer_ReturnsNull()
    {
        // Dave is unknown to Brand B (no mapping) even though he exists.
        (await _batchService.GetCustomerVouchersAsync(DaveCustomerId, BrandBId)).Should().BeNull();
        (await _batchService.GetCustomerVouchersAsync(Guid.NewGuid(), BrandAId)).Should().BeNull();
    }

    // ----- Controller endpoints -----

    [Fact]
    public async Task DistributionBatchesController_ListAndDetail_BrandManagerOnly()
    {
        var result = await RunPromotionAsync();
        var manager = new DistributionBatchesController(
            _batchService, new TestCurrentUserService("BrandManager", BrandAId));

        var list = await manager.ListBatches(PromoPlanId, CancellationToken.None);
        var ok = list.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<IReadOnlyList<DistributionBatchSummary>>()
            .Which.Should().ContainSingle();

        var detail = await manager.GetBatch(result.BatchId!.Value, CancellationToken.None);
        detail.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();

        // Cross-brand detail -> NotFound; missing brand context -> Unauthorized.
        var otherBrand = new DistributionBatchesController(
            _batchService, new TestCurrentUserService("BrandManager", BrandBId));
        (await otherBrand.GetBatch(result.BatchId!.Value, CancellationToken.None))
            .Should().BeOfType<Microsoft.AspNetCore.Mvc.NotFoundResult>();

        var noBrand = new DistributionBatchesController(_batchService, new TestCurrentUserService("Admin"));
        (await noBrand.ListBatches(PromoPlanId, CancellationToken.None))
            .Should().BeOfType<Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult>();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
