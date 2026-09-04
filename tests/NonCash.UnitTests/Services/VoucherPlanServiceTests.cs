using FluentAssertions;
using NSubstitute;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;

namespace NonCash.UnitTests.Services;

/// <summary>
/// Epic 3: verifies the applicability scope (companies/brands/outlets) a plan is created/updated with.
/// The scope is stored as a single jsonb owned entity; company + brand are derived server-side from the
/// owning brand, outlets come from the request and may be empty (empty = "whole brand").
/// </summary>
public class VoucherPlanServiceTests
{
    private readonly IVoucherPlanRepository _planRepository = Substitute.For<IVoucherPlanRepository>();
    private readonly IOutletRepository _outletRepository = Substitute.For<IOutletRepository>();
    private readonly IBrandRepository _brandRepository = Substitute.For<IBrandRepository>();
    private readonly VoucherPlanService _sut;

    private static readonly Guid BrandId = Guid.NewGuid();
    private static readonly Guid BusinessId = Guid.NewGuid();
    private static readonly Guid CreatorId = Guid.NewGuid();

    public VoucherPlanServiceTests()
    {
        _sut = new VoucherPlanService(_planRepository, _outletRepository, _brandRepository);
        _brandRepository.GetByIdAsync(BrandId, Arg.Any<CancellationToken>())
            .Returns(new Brand { Id = BrandId, BusinessId = BusinessId, Name = "Test Brand", TaxCode = "TAX1" });
    }

    private static CreatePlanDto ValidCreateDto(List<Guid> outletIds) => new(
        PlanDate: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        VoucherType: VoucherType.Gift,
        ValueType: VoucherValueType.Value,
        FaceValue: 100000m,
        NetValue: 90000m,
        ExpiryDate: new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        PublishDate: new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
        ValidFrom: null,
        ValidTo: null,
        TargetQuantity: 100,
        Budget: 10000000m,
        ImageUrl: null,
        IconUrl: null,
        OutletIds: outletIds);

    [Fact]
    public async Task CreateAsync_WithOutlets_PopulatesScopeWithCompanyBrandAndOutlets()
    {
        // Arrange
        var outletId = Guid.NewGuid();
        _outletRepository.ListByBrandAsync(BrandId, Arg.Any<CancellationToken>())
            .Returns(new List<Outlet> { new() { Id = outletId, BrandId = BrandId, Name = "Outlet 1" } });

        // Act
        var result = await _sut.CreateAsync(ValidCreateDto(new List<Guid> { outletId }), CreatorId, BrandId);

        // Assert
        result.Success.Should().BeTrue();
        result.Plan!.Scope.Companies.Should().Equal(BusinessId);
        result.Plan.Scope.Brands.Should().Equal(BrandId);
        result.Plan.Scope.Outlets.Should().Equal(outletId);
        await _planRepository.Received(1).AddAsync(Arg.Any<VoucherPlanHeader>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithoutOutlets_ScopeHasCompanyAndBrandButEmptyOutlets()
    {
        // Act — empty outlet scope is legitimate ("whole brand"); no outlet-ownership lookup happens.
        var result = await _sut.CreateAsync(ValidCreateDto(new List<Guid>()), CreatorId, BrandId);

        // Assert
        result.Success.Should().BeTrue();
        result.Plan!.Scope.Companies.Should().Equal(BusinessId);
        result.Plan.Scope.Brands.Should().Equal(BrandId);
        result.Plan.Scope.Outlets.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateDraftAsync_RefreshesScopeOutlets()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var outletId = Guid.NewGuid();
        var existing = new VoucherPlanHeader
        {
            Id = planId,
            BrandId = BrandId,
            ApprovalStatus = ApprovalStatus.Pending,
            PlanDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ExpiryDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            PublishDate = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            FaceValue = 100000m,
            NetValue = 90000m,
            TargetQuantity = 100,
            Scope = new VoucherScope { Companies = { BusinessId }, Brands = { BrandId } }
        };
        _planRepository.GetByIdWithOutletsAsync(planId, Arg.Any<CancellationToken>()).Returns(existing);
        _outletRepository.ListByBrandAsync(BrandId, Arg.Any<CancellationToken>())
            .Returns(new List<Outlet> { new() { Id = outletId, BrandId = BrandId, Name = "Outlet 1" } });

        var dto = new UpdatePlanDto(
            PlanDate: existing.PlanDate,
            VoucherType: VoucherType.Gift,
            ValueType: VoucherValueType.Value,
            FaceValue: 100000m,
            NetValue: 90000m,
            ExpiryDate: existing.ExpiryDate,
            PublishDate: existing.PublishDate,
            ValidFrom: null,
            ValidTo: null,
            TargetQuantity: 100,
            Budget: 10000000m,
            ImageUrl: null,
            IconUrl: null,
            OutletIds: new List<Guid> { outletId });

        // Act
        var result = await _sut.UpdateDraftAsync(planId, dto, BrandId);

        // Assert
        result.Success.Should().BeTrue();
        result.Plan!.Scope.Companies.Should().Equal(BusinessId);
        result.Plan.Scope.Brands.Should().Equal(BrandId);
        result.Plan.Scope.Outlets.Should().Equal(outletId);
    }
}
