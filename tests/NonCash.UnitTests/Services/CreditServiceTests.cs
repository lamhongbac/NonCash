using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;
using NonCash.Infrastructure.Services;

namespace NonCash.UnitTests.Services;

/// <summary>
/// Epic 10 — batch-model credit service. Balance = SUM(RemainingAmount) over
/// non-expired batches; consumption drains FIFO; billing never throws (grace policy).
/// </summary>
public class CreditServiceTests
{
    private readonly ApplicationDbContext _context;
    private readonly StubPolicyService _policyStub;
    private readonly StubWelcomePolicyService _welcomeStub;
    private readonly CreditService _sut;
    private readonly Guid _brandId = Guid.NewGuid();
    private readonly Guid _businessId = Guid.NewGuid();

    public CreditServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        // GrantWelcomeAsync resolves the brand's BusinessId, then the business's welcome
        // policy — seed a Business + Brand so that lookup resolves for welcome-dependent tests.
        _context.Businesses.Add(new Business
        {
            Id = _businessId,
            BusinessName = "Test Business",
            TaxCode = "TB-US",
            Address = "Test Address",
            IsActive = true
        });
        _context.Brands.Add(new Brand
        {
            Id = _brandId,
            BusinessId = _businessId,
            Name = "Test Brand",
            TaxCode = "TBR",
            Status = BrandStatus.Active
        });
        _context.SaveChanges();

        _policyStub = new StubPolicyService(new ResolvedCreditPolicy(
            PolicyId: Guid.NewGuid(),
            Name: "Test Policy",
            Scope: PolicyScope.Global,
            PricePerCreditVnd: 5000m,
            CreditExpiryMonths: 12,
            LowBalanceWarningPct: 20,
            ExpiryWarningDays: 30,
            AdjustmentApprovalThreshold: 1000));
        _welcomeStub = new StubWelcomePolicyService(new ResolvedWelcomePolicy(
            PolicyId: Guid.NewGuid(),
            Name: "Test Welcome",
            WelcomeCredits: 500,
            WelcomeCreditExpiryMonths: 12));
        _sut = new CreditService(_context, _policyStub, _welcomeStub, NullLogger<CreditService>.Instance);
    }

    /// <summary>Seeds a batch directly (sync SaveChanges keeps the custom CreatedAt).</summary>
    private CreditBatch SeedBatch(
        Guid brandId,
        int remaining,
        DateTime createdAt,
        DateTime? expiresAt = null,
        CreditBatchType type = CreditBatchType.Purchase)
    {
        var batch = new CreditBatch
        {
            Id = Guid.NewGuid(),
            BrandId = brandId,
            BatchType = type,
            OriginalAmount = remaining,
            RemainingAmount = remaining,
            PricePerCreditVnd = 0m,
            TotalPaidVnd = 0m,
            ExpiresAt = expiresAt,
            CreatedAt = createdAt
        };
        _context.CreditBatches.Add(batch);
        _context.SaveChanges();
        return batch;
    }

    // ----- balance -----

    [Fact]
    public async Task GetBalanceAsync_SumsRemainingOfNonExpiredBatches()
    {
        await _sut.GrantWelcomeAsync(_brandId);                          // +500
        await _sut.CreatePurchaseAsync(_brandId, 100, "bank ref", null, null); // +100
        await _sut.TryConsumeAsync(_brandId, Guid.NewGuid());            // -1

        var balance = await _sut.GetBalanceAsync(_brandId);

        balance.Should().Be(599);
    }

    [Fact]
    public async Task GetBalanceAsync_ExcludesExpiredBatches()
    {
        SeedBatch(_brandId, 50, DateTime.UtcNow.AddMonths(-13), expiresAt: DateTime.UtcNow.AddDays(-1));
        SeedBatch(_brandId, 10, DateTime.UtcNow);

        var balance = await _sut.GetBalanceAsync(_brandId);

        balance.Should().Be(10);
    }

    [Fact]
    public async Task GetBalanceAsync_WithNoBatches_ReturnsZero()
    {
        var balance = await _sut.GetBalanceAsync(_brandId);

        balance.Should().Be(0);
    }

    [Fact]
    public async Task GetBalanceAsync_IsScopedPerBrand()
    {
        var otherBrandId = Guid.NewGuid();
        SeedBatch(_brandId, 10, DateTime.UtcNow);
        SeedBatch(otherBrandId, 99, DateTime.UtcNow);

        var balance = await _sut.GetBalanceAsync(_brandId);

        balance.Should().Be(10);
    }

    [Fact]
    public async Task HasCreditAsync_TrueWhenPositive()
    {
        SeedBatch(_brandId, 1, DateTime.UtcNow);

        (await _sut.HasCreditAsync(_brandId)).Should().BeTrue();
    }

    [Fact]
    public async Task HasCreditAsync_FalseWhenZero()
    {
        (await _sut.HasCreditAsync(_brandId)).Should().BeFalse();
    }

    // ----- consumption -----

    [Fact]
    public async Task TryConsumeAsync_DrainsOldestBatchFirst()
    {
        var older = SeedBatch(_brandId, 5, DateTime.UtcNow.AddDays(-2));
        var newer = SeedBatch(_brandId, 5, DateTime.UtcNow);
        var voucherId = Guid.NewGuid();

        await _sut.TryConsumeAsync(_brandId, voucherId, "Sale order 123");

        (await _context.CreditBatches.FindAsync(older.Id))!.RemainingAmount.Should().Be(4);
        (await _context.CreditBatches.FindAsync(newer.Id))!.RemainingAmount.Should().Be(5);
        var consumption = await _context.CreditConsumptions.SingleAsync();
        consumption.BatchId.Should().Be(older.Id);
        consumption.BrandId.Should().Be(_brandId);
        consumption.VoucherDetailId.Should().Be(voucherId);
        consumption.Reference.Should().Be("Sale order 123");
    }

    [Fact]
    public async Task TryConsumeAsync_SkipsExpiredBatches()
    {
        var expired = SeedBatch(_brandId, 5, DateTime.UtcNow.AddDays(-2), expiresAt: DateTime.UtcNow.AddDays(-1));
        var valid = SeedBatch(_brandId, 5, DateTime.UtcNow);

        await _sut.TryConsumeAsync(_brandId, Guid.NewGuid());

        (await _context.CreditBatches.FindAsync(expired.Id))!.RemainingAmount.Should().Be(5);
        (await _context.CreditBatches.FindAsync(valid.Id))!.RemainingAmount.Should().Be(4);
    }

    [Fact]
    public async Task TryConsumeAsync_DuplicateVoucher_IsIdempotent()
    {
        SeedBatch(_brandId, 5, DateTime.UtcNow);
        var voucherId = Guid.NewGuid();

        await _sut.TryConsumeAsync(_brandId, voucherId);
        await _sut.TryConsumeAsync(_brandId, voucherId);

        // 1 voucher = max 1 credit, ever
        (await _context.CreditConsumptions.CountAsync()).Should().Be(1);
        (await _sut.GetBalanceAsync(_brandId)).Should().Be(4);
    }

    [Fact]
    public async Task TryConsumeAsync_GraceOverdraft_DrivesNewestBatchNegative()
    {
        // All credits used up — grace overdraft: consumption must still succeed silently.
        var drained = SeedBatch(_brandId, 0, DateTime.UtcNow);

        var act = () => _sut.TryConsumeAsync(_brandId, Guid.NewGuid());

        await act.Should().NotThrowAsync();
        (await _context.CreditBatches.FindAsync(drained.Id))!.RemainingAmount.Should().Be(-1);
        (await _sut.GetBalanceAsync(_brandId)).Should().Be(-1);
        (await _sut.HasCreditAsync(_brandId)).Should().BeFalse();
    }

    [Fact]
    public async Task TryConsumeAsync_WithNoBatches_RecordsNothing()
    {
        var act = () => _sut.TryConsumeAsync(_brandId, Guid.NewGuid());

        await act.Should().NotThrowAsync();
        (await _context.CreditConsumptions.CountAsync()).Should().Be(0);
        (await _sut.GetBalanceAsync(_brandId)).Should().Be(0);
    }

    // ----- purchase -----

    [Fact]
    public async Task CreatePurchaseAsync_SnapshotsPriceAndExpiryFromPolicy()
    {
        var adminId = Guid.NewGuid();

        var batch = await _sut.CreatePurchaseAsync(_brandId, 100, "bank transfer #42", "https://msa/slip.jpg", adminId);

        batch.BatchType.Should().Be(CreditBatchType.Purchase);
        batch.OriginalAmount.Should().Be(100);
        batch.RemainingAmount.Should().Be(100);
        batch.PricePerCreditVnd.Should().Be(5000m);
        batch.TotalPaidVnd.Should().Be(500000m);
        batch.PolicyId.Should().Be(_policyStub.Policy.PolicyId);
        batch.ExpiresAt.Should().NotBeNull();
        batch.ExpiresAt!.Value.Should().BeCloseTo(DateTime.UtcNow.AddMonths(12), TimeSpan.FromMinutes(1));
        batch.EvidenceImageUrl.Should().Be("https://msa/slip.jpg");
        batch.Reference.Should().Be("bank transfer #42");
        batch.CreatedBy.Should().Be(adminId);
    }

    [Fact]
    public async Task CreatePurchaseAsync_WithoutExpiryMonths_HasNoExpiry()
    {
        _policyStub.Policy = _policyStub.Policy with { CreditExpiryMonths = null };

        var batch = await _sut.CreatePurchaseAsync(_brandId, 10, null, null, null);

        batch.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task CreatePurchaseAsync_NonPositiveAmount_Throws()
    {
        var actZero = () => _sut.CreatePurchaseAsync(_brandId, 0, null, null, null);
        var actNegative = () => _sut.CreatePurchaseAsync(_brandId, -5, null, null, null);

        await actZero.Should().ThrowAsync<ArgumentException>().WithParameterName("amount");
        await actNegative.Should().ThrowAsync<ArgumentException>().WithParameterName("amount");
    }

    // ----- welcome grant -----

    [Fact]
    public async Task GrantWelcomeAsync_CreatesFreeBatchFromPolicy()
    {
        var batch = await _sut.GrantWelcomeAsync(_brandId);

        batch.Should().NotBeNull();
        batch!.BatchType.Should().Be(CreditBatchType.WelcomeGrant);
        batch.OriginalAmount.Should().Be(500);
        batch.RemainingAmount.Should().Be(500);
        batch.PricePerCreditVnd.Should().Be(0m);
        batch.TotalPaidVnd.Should().Be(0m);
        batch.ExpiresAt!.Value.Should().BeCloseTo(DateTime.UtcNow.AddMonths(12), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GrantWelcomeAsync_SecondCall_ReturnsNull()
    {
        await _sut.GrantWelcomeAsync(_brandId);

        var second = await _sut.GrantWelcomeAsync(_brandId);

        second.Should().BeNull();
        (await _context.CreditBatches.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GrantWelcomeAsync_ZeroWelcomeCredits_ReturnsNull()
    {
        _welcomeStub.Welcome = _welcomeStub.Welcome with { WelcomeCredits = 0 };

        var batch = await _sut.GrantWelcomeAsync(_brandId);

        batch.Should().BeNull();
    }

    // ----- adjustment batches -----

    [Fact]
    public async Task CreateAdjustmentBatchAsync_PurchaseOrWelcomeType_Throws()
    {
        var actPurchase = () => _sut.CreateAdjustmentBatchAsync(NewRequest(CreditBatchType.Purchase, 10));
        var actWelcome = () => _sut.CreateAdjustmentBatchAsync(NewRequest(CreditBatchType.WelcomeGrant, 10));

        await actPurchase.Should().ThrowAsync<ArgumentException>().WithParameterName("request");
        await actWelcome.Should().ThrowAsync<ArgumentException>().WithParameterName("request");
    }

    [Fact]
    public async Task CreateAdjustmentBatchAsync_NonPositiveAmount_Throws()
    {
        var act = () => _sut.CreateAdjustmentBatchAsync(NewRequest(CreditBatchType.Grant, 0));

        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("request");
    }

    [Fact]
    public async Task CreateAdjustmentBatchAsync_Clawback_CreatesNegativeNonExpiringBatch()
    {
        SeedBatch(_brandId, 100, DateTime.UtcNow);
        var request = NewRequest(CreditBatchType.Clawback, 30);

        var batch = await _sut.CreateAdjustmentBatchAsync(request);

        batch.OriginalAmount.Should().Be(-30);
        batch.RemainingAmount.Should().Be(-30);
        batch.ExpiresAt.Should().BeNull();
        batch.AdjustmentRequestId.Should().Be(request.Id);
        batch.CreatedBy.Should().Be(request.RequestedBy);
        (await _sut.GetBalanceAsync(_brandId)).Should().Be(70);
    }

    [Fact]
    public async Task CreateAdjustmentBatchAsync_Grant_CreatesPositiveBatchWithPolicyExpiry()
    {
        var batch = await _sut.CreateAdjustmentBatchAsync(NewRequest(CreditBatchType.Grant, 25));

        batch.BatchType.Should().Be(CreditBatchType.Grant);
        batch.OriginalAmount.Should().Be(25);
        batch.PricePerCreditVnd.Should().Be(0m);
        batch.ExpiresAt!.Value.Should().BeCloseTo(DateTime.UtcNow.AddMonths(12), TimeSpan.FromMinutes(1));
        batch.Reference.Should().Be("test reason");
    }

    private CreditAdjustmentRequest NewRequest(CreditBatchType type, int amount) => new()
    {
        Id = Guid.NewGuid(),
        BrandId = _brandId,
        AdjustmentType = type,
        Amount = amount,
        ReasonText = "test reason",
        RequestedBy = Guid.NewGuid()
    };

    // ----- queries -----

    [Fact]
    public async Task GetBatchesAsync_FiltersByBrandAndType_AndPaginates()
    {
        var otherBrandId = Guid.NewGuid();
        // GetBatchesAsync includes the Brand navigation — _brandId is seeded in the ctor;
        // add the second brand row for the join.
        _context.Brands.Add(new Brand
        {
            Id = otherBrandId,
            BusinessId = _businessId,
            Name = "Brand B",
            TaxCode = "TAX-B",
            Status = BrandStatus.Active
        });
        await _context.SaveChangesAsync();
        SeedBatch(_brandId, 500, DateTime.UtcNow.AddDays(-3), type: CreditBatchType.WelcomeGrant);
        SeedBatch(_brandId, 100, DateTime.UtcNow.AddDays(-2), type: CreditBatchType.Purchase);
        SeedBatch(_brandId, 20, DateTime.UtcNow.AddDays(-1), type: CreditBatchType.Grant);
        SeedBatch(otherBrandId, 500, DateTime.UtcNow, type: CreditBatchType.Purchase);

        var all = await _sut.GetBatchesAsync(new CreditBatchFilters { BrandId = _brandId });
        var purchasesOnly = await _sut.GetBatchesAsync(new CreditBatchFilters
        {
            BrandId = _brandId,
            BatchType = CreditBatchType.Purchase
        });
        var pageOne = await _sut.GetBatchesAsync(new CreditBatchFilters
        {
            BrandId = _brandId,
            Page = 1,
            PageSize = 2
        });

        all.TotalCount.Should().Be(3);
        purchasesOnly.TotalCount.Should().Be(1);
        purchasesOnly.Batches.Single().OriginalAmount.Should().Be(100);
        pageOne.Batches.Should().HaveCount(2);
        pageOne.TotalCount.Should().Be(3);
        // Newest first
        pageOne.Batches[0].BatchType.Should().Be(CreditBatchType.Grant);
    }

    [Fact]
    public async Task GetConsumptionsAsync_ScopedPerBrand_AndPaginates()
    {
        var otherBrandId = Guid.NewGuid();
        SeedBatch(_brandId, 10, DateTime.UtcNow);
        SeedBatch(otherBrandId, 10, DateTime.UtcNow);
        await _sut.TryConsumeAsync(_brandId, Guid.NewGuid());
        await _sut.TryConsumeAsync(_brandId, Guid.NewGuid());
        await _sut.TryConsumeAsync(_brandId, Guid.NewGuid());
        await _sut.TryConsumeAsync(otherBrandId, Guid.NewGuid());

        var all = await _sut.GetConsumptionsAsync(_brandId);
        var pageOne = await _sut.GetConsumptionsAsync(_brandId, page: 1, pageSize: 2);

        all.TotalCount.Should().Be(3);
        all.Consumptions.Should().OnlyContain(c => c.BrandId == _brandId);
        pageOne.Consumptions.Should().HaveCount(2);
        pageOne.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetExpiringBatchesAsync_ReturnsOnlyWindowedBatchesWithRemaining()
    {
        var inWindow = SeedBatch(_brandId, 5, DateTime.UtcNow, expiresAt: DateTime.UtcNow.AddDays(10));
        SeedBatch(_brandId, 5, DateTime.UtcNow, expiresAt: DateTime.UtcNow.AddDays(60));  // beyond window
        SeedBatch(_brandId, 5, DateTime.UtcNow, expiresAt: DateTime.UtcNow.AddDays(-1));  // already expired
        SeedBatch(_brandId, 5, DateTime.UtcNow);                                          // never expires
        SeedBatch(_brandId, 0, DateTime.UtcNow, expiresAt: DateTime.UtcNow.AddDays(5));   // nothing left

        var expiring = await _sut.GetExpiringBatchesAsync(_brandId, withinDays: 30);

        expiring.Should().ContainSingle().Which.Id.Should().Be(inWindow.Id);
    }

    /// <summary>Policy stub — CreditService only calls ResolveForBrandAsync.</summary>
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

    /// <summary>Welcome-policy stub — CreditService only calls ResolveForBusinessAsync.</summary>
    private sealed class StubWelcomePolicyService : IWelcomePolicyService
    {
        public ResolvedWelcomePolicy Welcome { get; set; }

        public StubWelcomePolicyService(ResolvedWelcomePolicy welcome) => Welcome = welcome;

        public Task<ResolvedWelcomePolicy> ResolveForBusinessAsync(Guid businessId, CancellationToken cancellationToken = default)
            => Task.FromResult(Welcome);

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
