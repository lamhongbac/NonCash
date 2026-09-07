using System.Reflection;
using System.Text.Json;
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
/// Epic 10 — prepaid credit billing on the batch model. Uses SQLite in-memory (relational)
/// so that the POS lock/commit flow (ExecuteUpdate), FK constraints and the unique
/// voucher-consumption index are enforced.
/// </summary>
public class CreditsControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _context;
    private readonly StubPolicyService _policyStub;
    private readonly StubWelcomePolicyService _welcomeStub;
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

        // PolicyId = null → CreditConfig-fallback pricing, no FK row needed.
        _policyStub = new StubPolicyService(new ResolvedCreditPolicy(
            PolicyId: null,
            Name: "Test Policy",
            Scope: null,
            PricePerCreditVnd: 5000m,
            CreditExpiryMonths: 12,
            LowBalanceWarningPct: 20,
            ExpiryWarningDays: 30,
            AdjustmentApprovalThreshold: 1000));
        _welcomeStub = new StubWelcomePolicyService(new ResolvedWelcomePolicy(
            PolicyId: null,
            Name: "Test Welcome",
            WelcomeCredits: 500,
            WelcomeCreditExpiryMonths: 12));
        _creditService = new CreditService(_context, _policyStub, _welcomeStub, new StubNotificationService(), NullLogger<CreditService>.Instance);

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

        // Distribute recipients must pre-exist: unknown phones are skipped, never auto-created.
        for (var i = 1; i <= 3; i++)
        {
            _context.Customers.Add(new Customer
            {
                PhoneNumber = $"091200000{i}",
                FullName = $"Distribute Target {i}",
                Status = CustomerStatus.Active
            });
        }

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
        return new CreditsController(_creditService, _policyStub, new FakeCurrentUserService(role, brandId));
    }

    private VoucherPlanHeader SeedPlan(Guid brandId, VoucherType voucherType, int voucherCount, bool memberOwned = false, ApprovalStatus approvalStatus = ApprovalStatus.Approved)
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
            ApprovalStatus = approvalStatus,
            VersionNumber = 1,
            // Epic 3: applicability scope now stored as jsonb on the plan (was the plan_outlets join table).
            Scope = new VoucherScope
            {
                Brands = new List<Guid> { brandId },
                Outlets = new List<Guid> { _outletId }
            }
        };
        _context.VoucherPlanHeaders.Add(plan);
        _context.SaveChanges();

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

    // ----- balance & batch scoping -----

    [Fact]
    public async Task GetBalance_BrandUser_ReturnsOwnBrandBalance()
    {
        await _creditService.CreatePurchaseAsync(_brandAId, 500, "welcome", null, null);
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
        await _creditService.CreatePurchaseAsync(_brandBId, 42, null, null, null);
        var controller = CreateController("Admin", null);

        var result = await controller.GetBalance(_brandBId, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<CreditBalanceResponse>().Subject;
        response.BrandId.Should().Be(_brandBId);
        response.Balance.Should().Be(42);
    }

    [Fact]
    public async Task GetBatches_BrandUser_SeesOwnBatchesOnly()
    {
        await _creditService.CreatePurchaseAsync(_brandAId, 500, null, null, null);
        await _creditService.CreatePurchaseAsync(_brandBId, 999, null, null, null);
        var controller = CreateController("BrandManager", _brandAId);

        var result = await controller.GetBatches(null, null, null, null, 1, 50, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<CreditBatchListResponse>().Subject;
        response.Batches.Should().OnlyContain(b => b.BrandId == _brandAId);
        response.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPricing_ReturnsResolvedPolicy()
    {
        var controller = CreateController("BrandManager", _brandAId);

        var result = await controller.GetPricing(null, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<ResolvedPolicyResponse>().Subject;
        response.PricePerCreditVnd.Should().Be(5000m);
        response.CreditExpiryMonths.Should().Be(12);
        response.Name.Should().Be("Test Policy");
    }

    // ----- top-up (purchase) -----

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
    public async Task TopUp_Admin_CreatesPurchaseBatchWithPriceSnapshot()
    {
        var controller = CreateController("Admin", null);
        var request = new CreditPurchaseRequest(_brandAId, 300, "bank transfer #7", "https://msa/slip-7.jpg");

        var result = await controller.TopUp(request, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var batch = ok.Value.Should().BeOfType<CreditBatchDto>().Subject;
        batch.BatchType.Should().Be("Purchase");
        batch.OriginalAmount.Should().Be(300);
        batch.RemainingAmount.Should().Be(300);
        batch.PricePerCreditVnd.Should().Be(5000m);
        batch.TotalPaidVnd.Should().Be(1500000m);
        batch.EvidenceImageUrl.Should().Be("https://msa/slip-7.jpg");
        (await _creditService.GetBalanceAsync(_brandAId)).Should().Be(300);
    }

    [Fact]
    public async Task TopUp_WithNonPositiveAmount_ReturnsBadRequest()
    {
        var controller = CreateController("Admin", null);

        var zero = await controller.TopUp(new CreditPurchaseRequest(_brandAId, 0, null, null), CancellationToken.None);
        var negative = await controller.TopUp(new CreditPurchaseRequest(_brandAId, -10, null, null), CancellationToken.None);

        zero.Result.Should().BeOfType<BadRequestObjectResult>();
        negative.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TopUp_WithEmptyBrandId_ReturnsBadRequest()
    {
        var controller = CreateController("Admin", null);

        var result = await controller.TopUp(new CreditPurchaseRequest(Guid.Empty, 10, null, null), CancellationToken.None);

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
        new BrandCustomerRepository(_context));

    private PosService CreatePosService() => new(
        new Repository<VoucherPlanDetail>(_context),
        new VoucherPlanRepository(_context),
        new Repository<Outlet>(_context),
        new BrandRepository(_context),
        new VoucherCodeService(),
        new VoucherLockRepository(_context),
        new SettlementService(_context),
        new VoucherEventPublisher(_context),
        new MemberAccountRepository(_context),
        new BrandCustomerRepository(_context));

    private ApprovalService CreateApprovalService() => new(
        new VoucherPlanRepository(_context),
        new Repository<VoucherReview>(_context),
        new UserAccountRepository(_context),
        new StubNotificationService(),
        _creditService,
        new VoucherGenerationService(
            new VoucherPlanRepository(_context),
            new VoucherCodeService(),
            new Repository<VoucherPlanDetail>(_context)));

    private PromotionService CreatePromotionService() => new(
        new VoucherPlanRepository(_context),
        new Repository<VoucherPlanDetail>(_context),
        new CustomerRepository(_context),
        new MemberAccountRepository(_context),
        new Repository<VoucherDistribution>(_context),
        new Repository<VoucherUsage>(_context),
        new VoucherTransferRepository(_context),
        new Repository<Outlet>(_context),
        new StubNotificationService(),
        new BrandCustomerRepository(_context),
        new Repository<VoucherDistributionBatch>(_context));

    [Fact]
    public async Task ConfirmGiftPayment_DoesNotChargeCredits()
    {
        await _creditService.CreatePurchaseAsync(_brandAId, 10, "welcome", null, null);
        var plan = SeedPlan(_brandAId, VoucherType.Gift, voucherCount: 3);
        var purchaseService = CreatePurchaseService();

        var orderResult = await purchaseService.CreateOrderAsync(
            new CreateOrderInput(_memberId, plan.Id, 2, null, null));
        orderResult.Success.Should().BeTrue();

        var payResult = await purchaseService.ConfirmPaymentAsync(orderResult.Order!.Id);

        // Credits are charged once at plan approval, never at gift sale.
        payResult.Success.Should().BeTrue();
        payResult.AllocatedCount.Should().Be(2);
        (await _context.CreditConsumptions.CountAsync()).Should().Be(0);
        (await _creditService.GetBalanceAsync(_brandAId)).Should().Be(10);
    }

    [Fact]
    public async Task ConfirmGiftPayment_ReplayedConfirm_DoesNotChargeCredits()
    {
        await _creditService.CreatePurchaseAsync(_brandAId, 10, null, null, null);
        var plan = SeedPlan(_brandAId, VoucherType.Gift, voucherCount: 2);
        var purchaseService = CreatePurchaseService();
        var orderResult = await purchaseService.CreateOrderAsync(
            new CreateOrderInput(_memberId, plan.Id, 1, null, null));

        await purchaseService.ConfirmPaymentAsync(orderResult.Order!.Id);
        await purchaseService.ConfirmPaymentAsync(orderResult.Order!.Id); // idempotent replay

        // Sale never charges credits (approval does), replayed or not.
        (await _context.CreditConsumptions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task PosCommit_Complimentary_DoesNotChargeCredits()
    {
        await _creditService.CreatePurchaseAsync(_brandAId, 5, null, null, null);
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

        // Credits are charged once at plan approval, never at redemption.
        commitResult.Status.Should().Be("Success");
        (await _context.CreditConsumptions.CountAsync()).Should().Be(0);
        (await _creditService.GetBalanceAsync(_brandAId)).Should().Be(5);
    }

    [Fact]
    public async Task PosCommit_Gift_CreatesNoNewConsumption()
    {
        await _creditService.CreatePurchaseAsync(_brandAId, 5, null, null, null);
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

        // Neither sale nor redemption charges credits — approval does.
        commitResult.Status.Should().Be("Success");
        (await _context.CreditConsumptions.CountAsync()).Should().Be(0);
    }

    // ----- approval-time charging (credit-at-approval model) -----

    [Fact]
    public async Task Approve_ChargesTargetQuantity_AndRecordsPlanConsumption()
    {
        await _creditService.CreatePurchaseAsync(_brandAId, 10, "welcome", null, null);
        var plan = SeedPlan(_brandAId, VoucherType.Gift, voucherCount: 4, approvalStatus: ApprovalStatus.Pending);
        var approvalService = CreateApprovalService();

        var result = await approvalService.ApproveAsync(plan.Id, _staffUserId, _brandAId, "BrandManager", null);

        result.Success.Should().BeTrue();
        var consumptions = await _context.CreditConsumptions.ToListAsync();
        consumptions.Should().ContainSingle();
        consumptions[0].PlanId.Should().Be(plan.Id);
        consumptions[0].Quantity.Should().Be(4);
        consumptions[0].BatchId.Should().BeNull();
        consumptions[0].VoucherDetailId.Should().BeNull();
        (await _creditService.GetBalanceAsync(_brandAId)).Should().Be(6);
    }

    [Fact]
    public async Task Approve_InsufficientCredits_RejectedAndPlanStaysPending()
    {
        await _creditService.CreatePurchaseAsync(_brandAId, 2, "welcome", null, null);
        var plan = SeedPlan(_brandAId, VoucherType.Gift, voucherCount: 5, approvalStatus: ApprovalStatus.Pending);
        var approvalService = CreateApprovalService();

        var result = await approvalService.ApproveAsync(plan.Id, _staffUserId, _brandAId, "BrandManager", null);

        // Point 2 of the reusable check: refuse + do nothing (no state change) when short.
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("InsufficientCredits");
        (await _context.CreditConsumptions.CountAsync()).Should().Be(0);
        (await _creditService.GetBalanceAsync(_brandAId)).Should().Be(2);

        _context.ChangeTracker.Clear();
        var reloaded = await _context.VoucherPlanHeaders.SingleAsync(p => p.Id == plan.Id);
        reloaded.ApprovalStatus.Should().Be(ApprovalStatus.Pending);
    }

    [Fact]
    public async Task Approve_ReplayedAfterSuccess_DoesNotDoubleCharge()
    {
        await _creditService.CreatePurchaseAsync(_brandAId, 10, "welcome", null, null);
        var plan = SeedPlan(_brandAId, VoucherType.Gift, voucherCount: 4, approvalStatus: ApprovalStatus.Pending);
        var approvalService = CreateApprovalService();

        var first = await approvalService.ApproveAsync(plan.Id, _staffUserId, _brandAId, "BrandManager", null);
        var second = await approvalService.ApproveAsync(plan.Id, _staffUserId, _brandAId, "BrandManager", null);

        first.Success.Should().BeTrue();
        second.Success.Should().BeFalse();     // already Approved → Conflict, before any charge
        second.ErrorCode.Should().Be("Conflict");
        (await _context.CreditConsumptions.CountAsync(c => c.PlanId == plan.Id)).Should().Be(1);
        (await _creditService.GetBalanceAsync(_brandAId)).Should().Be(6);
    }

    [Fact]
    public async Task GetPlanFunding_ReusesCheck_ForOwnBrandPlan()
    {
        await _creditService.CreatePurchaseAsync(_brandAId, 7, null, null, null);
        var plan = SeedPlan(_brandAId, VoucherType.Gift, voucherCount: 3, approvalStatus: ApprovalStatus.Pending);
        var approvalService = CreateApprovalService();

        // Point 1 of the reusable check: informational funding read for the approve form.
        var funding = await approvalService.GetPlanFundingAsync(plan.Id, _brandAId);

        funding.Should().NotBeNull();
        funding!.Sufficient.Should().BeTrue();
        funding.Balance.Should().Be(7);
        funding.Required.Should().Be(3);
    }

    [Fact]
    public async Task TryConsumeForPlan_IsIdempotent_PerPlan()
    {
        await _creditService.CreatePurchaseAsync(_brandAId, 10, null, null, null);
        var planId = Guid.NewGuid();

        var first = await _creditService.TryConsumeForPlanAsync(_brandAId, planId, 4, "test");
        var second = await _creditService.TryConsumeForPlanAsync(_brandAId, planId, 4, "test");

        first.Should().BeTrue();
        second.Should().BeTrue();
        (await _context.CreditConsumptions.CountAsync(c => c.PlanId == planId)).Should().Be(1);
        (await _creditService.GetBalanceAsync(_brandAId)).Should().Be(6);
    }

    [Fact]
    public async Task TryConsumeForPlan_InsufficientBalance_ReturnsFalseAndChargesNothing()
    {
        await _creditService.CreatePurchaseAsync(_brandAId, 3, null, null, null);

        var ok = await _creditService.TryConsumeForPlanAsync(_brandAId, Guid.NewGuid(), 5, "test");

        ok.Should().BeFalse();
        (await _context.CreditConsumptions.CountAsync()).Should().Be(0);
        (await _creditService.GetBalanceAsync(_brandAId)).Should().Be(3);
    }

    [Fact]
    public async Task TryConsumeForPlan_DrainsSoonestExpiringBatchFirst()
    {
        await _creditService.CreatePurchaseAsync(_brandAId, 3, null, null, null);   // created (and expires) first
        await _creditService.CreatePurchaseAsync(_brandAId, 10, null, null, null);  // created (and expires) later

        var ok = await _creditService.TryConsumeForPlanAsync(_brandAId, Guid.NewGuid(), 5, "test");

        ok.Should().BeTrue();
        var batches = await _context.CreditBatches.ToListAsync();
        batches.Single(b => b.OriginalAmount == 3).RemainingAmount.Should().Be(0);   // soonest-expiring drained first
        batches.Single(b => b.OriginalAmount == 10).RemainingAmount.Should().Be(8);
        (await _creditService.GetBalanceAsync(_brandAId)).Should().Be(8);
    }

    [Fact]
    public async Task EvaluatePlanFunding_ReportsSufficiencyAgainstBalance()
    {
        await _creditService.CreatePurchaseAsync(_brandAId, 5, null, null, null);

        var enough = await _creditService.EvaluatePlanFundingAsync(_brandAId, 5);
        var shortByOne = await _creditService.EvaluatePlanFundingAsync(_brandAId, 6);

        enough.Sufficient.Should().BeTrue();
        enough.Balance.Should().Be(5);
        enough.Required.Should().Be(5);
        shortByOne.Sufficient.Should().BeFalse();
        shortByOne.Balance.Should().Be(5);
        shortByOne.Required.Should().Be(6);
    }

    // ----- guards: generation & ordering are NOT credit-gated (approval is) -----

    [Fact]
    public async Task GenerateBatch_NotBlockedByCreditBalance()
    {
        // No credit batch for the brand: generation still succeeds because credits
        // are charged at approval, not at generation.
        var plan = SeedPlan(_brandAId, VoucherType.Gift, voucherCount: 0);
        plan.TargetQuantity = 10;
        _context.SaveChanges();
        var generationService = new VoucherGenerationService(
            new VoucherPlanRepository(_context),
            new VoucherCodeService(),
            new Repository<VoucherPlanDetail>(_context));

        var result = await generationService.GenerateBatchAsync(plan.Id, 10, _brandAId);

        result.Success.Should().BeTrue();
        result.GeneratedCount.Should().Be(10);
        (await _context.CreditConsumptions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateOrder_NotBlockedByCreditBalance()
    {
        // No credit batch for the brand: ordering still succeeds because credits
        // are charged at approval, not at purchase.
        var plan = SeedPlan(_brandAId, VoucherType.Gift, voucherCount: 2);
        var purchaseService = CreatePurchaseService();

        var result = await purchaseService.CreateOrderAsync(
            new CreateOrderInput(_memberId, plan.Id, 1, null, null));

        result.Success.Should().BeTrue();
        (await _context.CreditConsumptions.CountAsync()).Should().Be(0);
    }

    // ----- Phase 1: approve + optional generation -----

    [Fact]
    public async Task Approve_WithGenerateFlag_CreatesTargetQuantityDetails_AndOnePlanConsumption()
    {
        await _creditService.CreatePurchaseAsync(_brandAId, 10, "welcome", null, null);
        // Approved quota of 4 but no detail rows yet — the checkbox materializes them at approval.
        var plan = SeedPlan(_brandAId, VoucherType.Gift, voucherCount: 0, approvalStatus: ApprovalStatus.Pending);
        plan.TargetQuantity = 4;
        _context.SaveChanges();
        var approvalService = CreateApprovalService();

        var result = await approvalService.ApproveAsync(plan.Id, _staffUserId, _brandAId, "BrandManager", null, generateVouchers: true);

        result.Success.Should().BeTrue();
        result.GeneratedCount.Should().Be(4);
        result.Warning.Should().BeNull();
        (await _context.VoucherPlanDetails.CountAsync(d => d.ParentId == plan.Id)).Should().Be(4);
        // Credits are still charged exactly once, at approval, for the full target quantity.
        (await _context.CreditConsumptions.CountAsync(c => c.PlanId == plan.Id)).Should().Be(1);
        (await _creditService.GetBalanceAsync(_brandAId)).Should().Be(6);
    }

    [Fact]
    public async Task Approve_WithoutGenerateFlag_CreatesNoDetails()
    {
        await _creditService.CreatePurchaseAsync(_brandAId, 10, "welcome", null, null);
        var plan = SeedPlan(_brandAId, VoucherType.Gift, voucherCount: 0, approvalStatus: ApprovalStatus.Pending);
        plan.TargetQuantity = 4;
        _context.SaveChanges();
        var approvalService = CreateApprovalService();

        var result = await approvalService.ApproveAsync(plan.Id, _staffUserId, _brandAId, "BrandManager", null);

        result.Success.Should().BeTrue();
        result.GeneratedCount.Should().Be(0);
        // Approval charges credits but does NOT create detail rows unless the checkbox is on.
        (await _context.VoucherPlanDetails.CountAsync(d => d.ParentId == plan.Id)).Should().Be(0);
        (await _context.CreditConsumptions.CountAsync(c => c.PlanId == plan.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Approve_ResponsePayload_SerializesApprovalStatusAsString()
    {
        await _creditService.CreatePurchaseAsync(_brandAId, 10, "welcome", null, null);
        var plan = SeedPlan(_brandAId, VoucherType.Gift, voucherCount: 0, approvalStatus: ApprovalStatus.Pending);
        plan.TargetQuantity = 2;
        _context.SaveChanges();
        var controller = new ApprovalsController(
            CreateApprovalService(), new FakeCurrentUserService("BrandManager", _brandAId, _staffUserId));

        var result = await controller.Approve(plan.Id, new ApproveRequest(null), CancellationToken.None);

        // Regression: the Blazor approve flow deserializes approvalStatus as string —
        // the API must not emit the raw enum number in the success payload.
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        using var doc = JsonDocument.Parse(
            JsonSerializer.Serialize(ok.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var status = doc.RootElement.GetProperty("approvalStatus");
        status.ValueKind.Should().Be(JsonValueKind.String);
        status.GetString().Should().Be("Approved");
    }

    // ----- usage-history enrichment & reconciliation summary -----

    [Fact]
    public async Task GetConsumptions_EnrichesPlanApprovalRows_WithPlanNameAndTotalQuantity()
    {
        await _creditService.CreatePurchaseAsync(_brandAId, 100, "topup", null, null);
        var plan = SeedPlan(_brandAId, VoucherType.Gift, voucherCount: 0, approvalStatus: ApprovalStatus.Pending);
        plan.DisplayName = "September Gift";
        plan.TargetQuantity = 20;
        _context.SaveChanges();
        var approvalService = CreateApprovalService();
        (await approvalService.ApproveAsync(plan.Id, _staffUserId, _brandAId, "BrandManager", null)).Success.Should().BeTrue();
        var controller = CreateController("BrandManager", _brandAId);

        var result = await controller.GetConsumptions(null, 1, 50, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<CreditConsumptionListResponse>().Subject;
        response.TotalCount.Should().Be(1);
        // The real billed amount is the SUM of quantities (20), not the row count (1).
        response.TotalQuantity.Should().Be(20);
        var row = response.Consumptions.Should().ContainSingle().Subject;
        row.PlanId.Should().Be(plan.Id);
        row.PlanName.Should().Be("September Gift");
        row.Quantity.Should().Be(20);
    }

    [Fact]
    public async Task GetSummary_ReturnsReconciliationTotals()
    {
        await _creditService.CreatePurchaseAsync(_brandAId, 100, "topup", null, null);
        await _creditService.GrantWelcomeAsync(_brandAId, sendNotification: false); // +500
        var plan = SeedPlan(_brandAId, VoucherType.Gift, voucherCount: 0, approvalStatus: ApprovalStatus.Pending);
        plan.TargetQuantity = 20;
        _context.SaveChanges();
        var approvalService = CreateApprovalService();
        (await approvalService.ApproveAsync(plan.Id, _staffUserId, _brandAId, "BrandManager", null)).Success.Should().BeTrue();
        var controller = CreateController("BrandManager", _brandAId);

        var result = await controller.GetSummary(null, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<CreditSummaryResponse>().Subject;
        response.BrandId.Should().Be(_brandAId);
        response.TotalGranted.Should().Be(600);
        response.TotalConsumed.Should().Be(20);
        response.Balance.Should().Be(580);
    }

    [Fact]
    public async Task GetSummary_BrandUser_RequestingOtherBrand_IsForbidden()
    {
        var controller = CreateController("BrandManager", _brandAId);

        var result = await controller.GetSummary(_brandBId, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    // ----- Phase 2: generation is capped at Remaining and never touches TargetDistributed -----

    [Fact]
    public async Task Generate_OverRemaining_IsRejected_AndDoesNotIncrementTargetDistributed()
    {
        var plan = SeedPlan(_brandAId, VoucherType.Gift, voucherCount: 0);
        plan.TargetQuantity = 10;
        _context.SaveChanges();
        var generationService = new VoucherGenerationService(
            new VoucherPlanRepository(_context),
            new VoucherCodeService(),
            new Repository<VoucherPlanDetail>(_context));

        var first = await generationService.GenerateBatchAsync(plan.Id, 6, _brandAId);
        var over = await generationService.GenerateBatchAsync(plan.Id, 5, _brandAId); // only 4 remain

        first.Success.Should().BeTrue();
        first.GeneratedCount.Should().Be(6);
        over.Success.Should().BeFalse();
        over.ErrorMessage.Should().Contain("QuantityExceedsRemaining");
        (await _context.VoucherPlanDetails.CountAsync(d => d.ParentId == plan.Id)).Should().Be(6);

        // Generation materializes the pool but is not a distribution: the header counter stays 0.
        _context.ChangeTracker.Clear();
        var reloaded = await _context.VoucherPlanHeaders.SingleAsync(p => p.Id == plan.Id);
        reloaded.TargetDistributed.Should().Be(0);
    }

    // ----- Phase 3: distribute drains the pool, reconciles the counter, and has no silent side effects -----

    [Fact]
    public async Task Distribute_Success_DrainsPool_WritesDistributions_ReconcilesCounter()
    {
        var plan = SeedPlan(_brandAId, VoucherType.Complimentary, voucherCount: 5); // 5 pending, unassigned
        var promotion = CreatePromotionService();

        var result = await promotion.DistributeAsync(plan.Id, _brandAId,
            new[] { "0912000001", "0912000002", "0912000003" });

        result.Success.Should().BeTrue();
        result.DistributedCount.Should().Be(3);
        (await _context.VoucherDistributions.CountAsync()).Should().Be(3);
        (await _context.VoucherPlanDetails.CountAsync(d => d.ParentId == plan.Id && d.MemberId != null)).Should().Be(3);

        // target_distributed equals the real distribution-row count (fixes historical inflation).
        _context.ChangeTracker.Clear();
        var reloaded = await _context.VoucherPlanHeaders.SingleAsync(p => p.Id == plan.Id);
        reloaded.TargetDistributed.Should().Be(3);
        (await _context.VoucherDistributions.CountAsync()).Should().Be(reloaded.TargetDistributed);
    }

    [Fact]
    public async Task Distribute_InsufficientStock_ReturnsError_AndCreatesNoCustomersOrAssignments()
    {
        var plan = SeedPlan(_brandAId, VoucherType.Complimentary, voucherCount: 2); // only 2 available
        var promotion = CreatePromotionService();
        var customersBefore = await _context.Customers.CountAsync();
        var membersBefore = await _context.MemberAccounts.CountAsync();
        var brandCustomersBefore = await _context.BrandCustomers.CountAsync();

        var result = await promotion.DistributeAsync(plan.Id, _brandAId,
            new[] { "0912000001", "0912000002", "0912000003" }); // 3 required > 2 available

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("InsufficientStock");
        result.ErrorMessage.Should().Contain("Nothing was distributed");

        // No silent side effects: nothing assigned, no distributions, no new customers/members/brand-customers.
        (await _context.VoucherDistributions.CountAsync()).Should().Be(0);
        (await _context.VoucherPlanDetails.CountAsync(d => d.ParentId == plan.Id && d.MemberId != null)).Should().Be(0);
        (await _context.Customers.CountAsync()).Should().Be(customersBefore);
        (await _context.MemberAccounts.CountAsync()).Should().Be(membersBefore);
        (await _context.BrandCustomers.CountAsync()).Should().Be(brandCustomersBefore);

        _context.ChangeTracker.Clear();
        var reloaded = await _context.VoucherPlanHeaders.SingleAsync(p => p.Id == plan.Id);
        reloaded.TargetDistributed.Should().Be(0);
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
        private readonly Guid? _userId;

        public FakeCurrentUserService(string role, Guid? brandId, Guid? userId = null)
        {
            _role = role;
            _brandId = brandId;
            _userId = userId;
        }

        public Guid? GetCurrentBrandId() => _brandId;
        public string? GetCurrentUserId() => (_userId ?? Guid.NewGuid()).ToString();
        public string? GetCurrentUserRole() => _role;
        public bool IsInRole(string role) => role == _role;
        public Guid? GetCurrentCustomerId() => null;
    }

    /// <summary>Policy stub — the credit flows only call ResolveForBrandAsync.</summary>
    private sealed class StubPolicyService : ICreditPolicyService
    {
        public ResolvedCreditPolicy Policy { get; set; }

        public StubPolicyService(ResolvedCreditPolicy policy) => Policy = policy;

        public Task<ResolvedCreditPolicy> ResolveForBrandAsync(Guid brandId, CancellationToken cancellationToken = default)
            => Task.FromResult(Policy);

        public Task<IReadOnlyList<CreditPricingPolicy>> GetPoliciesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<CreditPricingPolicy?> GetPolicyAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<CreditPricingPolicy> CreatePolicyAsync(CreditPricingPolicy policy, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<CreditPricingPolicy> UpdatePolicyAsync(Guid id, CreditPricingPolicy changes, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task DeactivatePolicyAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<BrandGroup>> GetGroupsAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<BrandGroup?> GetGroupAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<BrandGroup> CreateGroupAsync(string name, string? description, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<BrandGroup> UpdateGroupAsync(Guid id, string name, string? description, bool isActive, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task SetGroupMembersAsync(Guid groupId, IReadOnlyCollection<Guid> brandIds, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    /// <summary>No-op notification stub so CreditService unit tests are not affected by SMTP.</summary>
    private sealed class StubNotificationService : INotificationService
    {
        public Task NotifyAdminNewRegistrationAsync(Guid requestId, string companyName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyRegistrationRejectedAsync(string email, string businessName, string? reviewNotes = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyApplicantRegistrationSubmittedAsync(string email, string companyName, Guid requestId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyVoucherReceivedAsync(VoucherReceivedNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyAdjustmentPendingAsync(AdjustmentPendingNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyAdjustmentReviewedAsync(AdjustmentReviewedNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyCreditsExpiringAsync(CreditsExpiringNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyWelcomeCreditGrantedAsync(WelcomeCreditGrantedNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyBrandCreatedAsync(BrandCreatedNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyBusinessActivatedAsync(BusinessActivatedNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyContractSentAsync(ContractSentNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyCreditPurchasedAsync(CreditPurchasedNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyLowCreditBalanceAsync(LowCreditBalanceNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyCreditsForfeitedAsync(CreditsForfeitedNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyPlanReviewedAsync(PlanReviewedNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyStaffAccountCreatedAsync(StaffAccountCreatedNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyVoucherTransferInitiatedAsync(VoucherTransferInitiatedNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyPasswordResetAsync(PasswordResetNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>Welcome-policy stub — CreditService only calls ResolveForBusinessAsync.</summary>
    private sealed class StubWelcomePolicyService : IWelcomePolicyService
    {
        public ResolvedWelcomePolicy Welcome { get; set; }

        public StubWelcomePolicyService(ResolvedWelcomePolicy welcome) => Welcome = welcome;

        public Task<ResolvedWelcomePolicy> ResolveForBusinessAsync(Guid businessId, CancellationToken cancellationToken = default)
            => Task.FromResult(Welcome);

        public Task<WelcomeGrantPolicy> AssignTemplateToBusinessAsync(Guid businessId, Guid? templateId, Guid? actingUserId = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<WelcomeGrantPolicyTemplate>> GetTemplatesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<WelcomeGrantPolicyTemplate?> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<WelcomeGrantPolicyTemplate?> GetDefaultTemplateAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<WelcomeGrantPolicyTemplate> CreateTemplateAsync(WelcomeGrantPolicyTemplate template, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<WelcomeGrantPolicyTemplate> UpdateTemplateAsync(Guid id, WelcomeGrantPolicyTemplate changes, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task DeactivateTemplateAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task SetDefaultTemplateAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<WelcomeGrantPolicy>> GetPoliciesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<WelcomeGrantPolicy?> GetPolicyAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<WelcomeGrantPolicy> CreatePolicyAsync(WelcomeGrantPolicy policy, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<WelcomeGrantPolicy> UpdatePolicyAsync(Guid id, WelcomeGrantPolicy changes, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task DeactivatePolicyAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
