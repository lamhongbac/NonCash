using FluentAssertions;
using NSubstitute;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;

namespace NonCash.UnitTests.Services;

public class VoucherGenerationServiceTests
{
    private readonly IVoucherPlanRepository _planRepository = Substitute.For<IVoucherPlanRepository>();
    private readonly IVoucherCodeService _voucherCodeService = Substitute.For<IVoucherCodeService>();
    private readonly IRepository<VoucherPlanDetail> _detailRepository = Substitute.For<IRepository<VoucherPlanDetail>>();
    private readonly VoucherGenerationService _sut;

    private readonly Guid _planId = Guid.NewGuid();
    private readonly Guid _brandId = Guid.NewGuid();

    public VoucherGenerationServiceTests()
    {
        _sut = new VoucherGenerationService(_planRepository, _voucherCodeService, _detailRepository);
    }

    private VoucherPlanHeader ApprovedPlan(int targetQuantity) => new()
    {
        Id = _planId,
        BrandId = _brandId,
        ApprovalStatus = ApprovalStatus.Approved,
        TargetQuantity = targetQuantity,
        TargetDistributed = 0
    };

    private void ArrangePlan(VoucherPlanHeader plan, int existingDetails)
    {
        _planRepository.GetByIdWithOutletsAsync(_planId, Arg.Any<CancellationToken>()).Returns(plan);
        _voucherCodeService.GenerateSecretKey().Returns("SECRET");
        var existing = Enumerable.Range(1, existingDetails)
            .Select(i => new VoucherPlanDetail { Id = Guid.NewGuid(), ParentId = _planId, SerialNo = $"VC-X-{i:D8}" })
            .ToList();
        _detailRepository.FindAsync(Arg.Any<System.Linq.Expressions.Expression<System.Func<VoucherPlanDetail, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(existing);
    }

    [Fact]
    public async Task GenerateBatchAsync_QuantityExceedsRemaining_Fails()
    {
        // Arrange: plan approved for 50, none generated yet, request 51
        ArrangePlan(ApprovedPlan(50), 0);

        // Act
        var result = await _sut.GenerateBatchAsync(_planId, 51, _brandId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().StartWith("QuantityExceedsRemaining");
        await _detailRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateBatchAsync_QuantityEqualsRemaining_Succeeds()
    {
        // Arrange: plan approved for 50, none generated yet, request exactly 50
        ArrangePlan(ApprovedPlan(50), 0);

        // Act
        var result = await _sut.GenerateBatchAsync(_planId, 50, _brandId);

        // Assert
        result.Success.Should().BeTrue();
        result.GeneratedCount.Should().Be(50);
        await _detailRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateBatchAsync_PlanAlreadyFullyGenerated_Fails()
    {
        // Arrange: plan approved for 50, 50 already generated, request 1 more
        var plan = ApprovedPlan(50);
        plan.TargetDistributed = 50;
        ArrangePlan(plan, 50);

        // Act
        var result = await _sut.GenerateBatchAsync(_planId, 1, _brandId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().StartWith("QuantityExceedsRemaining");
        await _detailRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateBatchAsync_PartialBatchesWithinTarget_Succeed()
    {
        // Arrange: plan approved for 50, 20 already generated, request 30 (exactly the remainder)
        ArrangePlan(ApprovedPlan(50), 20);

        // Act
        var result = await _sut.GenerateBatchAsync(_planId, 30, _brandId);

        // Assert
        result.Success.Should().BeTrue();
        result.GeneratedCount.Should().Be(30);
    }

    [Fact]
    public async Task GenerateBatchAsync_SerialNoUsesPlanPrefix_NotBrandCode()
    {
        // Arrange: two plans from the same brand — serial numbers must not collide
        ArrangePlan(ApprovedPlan(10), 0);

        // Act
        await _sut.GenerateBatchAsync(_planId, 1, _brandId);

        // Assert: serial number uses plan prefix (first 8 hex chars of planId), not brand code
        var expectedPrefix = _planId.ToString()[..8].ToUpperInvariant();
        await _detailRepository.Received(1).AddAsync(
            Arg.Is<VoucherPlanDetail>(d => d.SerialNo.StartsWith($"VC-{expectedPrefix}-")),
            Arg.Any<CancellationToken>());
    }
}
