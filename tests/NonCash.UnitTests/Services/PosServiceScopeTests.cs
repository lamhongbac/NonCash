using System.Linq.Expressions;
using FluentAssertions;
using NSubstitute;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;

namespace NonCash.UnitTests.Services;

/// <summary>
/// Tests for PosService scope-check logic: empty Scope.Outlets = whole brand,
/// non-empty = explicit list only, cross-brand always rejected.
/// </summary>
public class PosServiceScopeTests
{
    private readonly IRepository<VoucherPlanDetail> _detailRepo = Substitute.For<IRepository<VoucherPlanDetail>>();
    private readonly IVoucherPlanRepository _planRepo = Substitute.For<IVoucherPlanRepository>();
    private readonly IRepository<Outlet> _outletRepo = Substitute.For<IRepository<Outlet>>();
    private readonly IBrandRepository _brandRepo = Substitute.For<IBrandRepository>();
    private readonly IVoucherCodeService _codeService = Substitute.For<IVoucherCodeService>();
    private readonly IVoucherLockRepository _lockRepo = Substitute.For<IVoucherLockRepository>();
    private readonly ISettlementService _settlement = Substitute.For<ISettlementService>();
    private readonly IVoucherEventPublisher _publisher = Substitute.For<IVoucherEventPublisher>();
    private readonly IMemberAccountRepository _memberRepo = Substitute.For<IMemberAccountRepository>();
    private readonly IBrandCustomerRepository _brandCustomerRepo = Substitute.For<IBrandCustomerRepository>();
    private readonly PosService _sut;

    private readonly Guid _brandId = Guid.NewGuid();
    private readonly Guid _planId = Guid.NewGuid();
    private readonly Guid _detailId = Guid.NewGuid();
    private readonly Guid _outletId = Guid.NewGuid();
    private readonly Guid _otherOutletId = Guid.NewGuid();

    public PosServiceScopeTests()
    {
        _sut = new PosService(
            _detailRepo, _planRepo, _outletRepo, _brandRepo, _codeService,
            _lockRepo, _settlement, _publisher, _memberRepo, _brandCustomerRepo);
    }

    private void ArrangeVoucher(VoucherScope scope, Guid outletIdForDetail)
    {
        var detail = new VoucherPlanDetail
        {
            Id = _detailId,
            ParentId = _planId,
            SerialNo = "VC-TEST-001",
            VoucherCodeSecret = "secret-key",
            UsageStatus = UsageStatus.Pending
        };

        _codeService.TryExtractVoucherId("test-code", out Arg.Any<Guid>())
            .Returns(x => { x[1] = _detailId; return true; });
        _codeService.CheckCode("test-code", "secret-key", out Arg.Any<Guid>())
            .Returns(x => { x[2] = _detailId; return VoucherCodeCheck.Valid; });
        _detailRepo.GetByIdAsync(_detailId, Arg.Any<CancellationToken>()).Returns(detail);

        var plan = new VoucherPlanHeader
        {
            Id = _planId,
            BrandId = _brandId,
            ApprovalStatus = ApprovalStatus.Approved,
            ExpiryDate = DateTime.UtcNow.AddDays(30),
            PublishDate = DateTime.UtcNow.AddDays(-1),
            Scope = scope
        };
        _planRepo.GetByIdWithOutletsAsync(_planId, Arg.Any<CancellationToken>()).Returns(plan);

        var outlet = new Outlet { Id = outletIdForDetail, BrandId = _brandId, Code = "CMT8", Name = "Test Outlet" };
        _outletRepo.GetByIdAsync(outletIdForDetail, Arg.Any<CancellationToken>()).Returns(outlet);
        // BuildInfoAsync resolves the scope's outlet names via FindAsync when the scope lists
        // explicit outlets; return this outlet so the name lookup is deterministic.
        _outletRepo.FindAsync(Arg.Any<Expression<Func<Outlet, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { outlet });

        var brand = new Brand { Id = _brandId, Name = "Test Brand" };
        _brandRepo.GetByIdAsync(_brandId, Arg.Any<CancellationToken>()).Returns(brand);
    }

    [Fact]
    public async Task VerifyAsync_EmptyScopeOutlets_SameBrandOutlet_ReturnsValid()
    {
        // Arrange: empty Outlets = whole brand
        ArrangeVoucher(new VoucherScope { Brands = { _brandId }, Outlets = new List<Guid>() }, _outletId);

        // Act
        var result = await _sut.VerifyAsync("test-code", _outletId);

        // Assert
        result.Status.Should().Be("Valid");
        result.Reason.Should().BeNull();
        // Empty scope = whole brand: no explicit outlet list, summary names the brand.
        result.VoucherInfo!.ScopeSummary.Should().Be("All stores of Test Brand");
        result.VoucherInfo.ApplicableOutlets.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyAsync_SpecificScopeOutlets_MatchingOutlet_ReturnsValid()
    {
        // Arrange: specific outlets list includes this outlet
        ArrangeVoucher(new VoucherScope { Brands = { _brandId }, Outlets = { _outletId } }, _outletId);

        // Act
        var result = await _sut.VerifyAsync("test-code", _outletId);

        // Assert
        result.Status.Should().Be("Valid");
        // Explicit scope: summary counts the stores and the resolved names are listed.
        result.VoucherInfo!.ScopeSummary.Should().Be("1 selected store(s)");
        result.VoucherInfo.ApplicableOutlets.Should().Equal("Test Outlet");
    }

    [Fact]
    public async Task VerifyAsync_SpecificScopeOutlets_NonMatchingOutlet_ReturnsOutletNotAuthorized()
    {
        // Arrange: specific outlets list does NOT include this outlet
        ArrangeVoucher(new VoucherScope { Brands = { _brandId }, Outlets = { _otherOutletId } }, _outletId);

        // Act
        var result = await _sut.VerifyAsync("test-code", _outletId);

        // Assert
        result.Status.Should().Be("Invalid");
        result.Reason.Should().Be("OutletNotAuthorized");
    }

    [Fact]
    public async Task VerifyAsync_CrossBrandOutlet_ReturnsOutletNotAuthorized()
    {
        // Arrange: outlet belongs to a different brand
        var otherBrandId = Guid.NewGuid();
        var detail = new VoucherPlanDetail
        {
            Id = _detailId,
            ParentId = _planId,
            SerialNo = "VC-TEST-002",
            VoucherCodeSecret = "secret-key",
            UsageStatus = UsageStatus.Pending
        };

        _codeService.TryExtractVoucherId("test-code", out Arg.Any<Guid>())
            .Returns(x => { x[1] = _detailId; return true; });
        _codeService.CheckCode("test-code", "secret-key", out Arg.Any<Guid>())
            .Returns(x => { x[2] = _detailId; return VoucherCodeCheck.Valid; });
        _detailRepo.GetByIdAsync(_detailId, Arg.Any<CancellationToken>()).Returns(detail);

        var plan = new VoucherPlanHeader
        {
            Id = _planId,
            BrandId = _brandId,
            ApprovalStatus = ApprovalStatus.Approved,
            ExpiryDate = DateTime.UtcNow.AddDays(30),
            PublishDate = DateTime.UtcNow.AddDays(-1),
            Scope = new VoucherScope { Brands = { _brandId }, Outlets = new List<Guid>() }
        };
        _planRepo.GetByIdWithOutletsAsync(_planId, Arg.Any<CancellationToken>()).Returns(plan);

        // Outlet from a DIFFERENT brand
        var outlet = new Outlet { Id = _outletId, BrandId = otherBrandId, Code = "OTHER", Name = "Other Brand Outlet" };
        _outletRepo.GetByIdAsync(_outletId, Arg.Any<CancellationToken>()).Returns(outlet);

        // Act
        var result = await _sut.VerifyAsync("test-code", _outletId);

        // Assert
        result.Status.Should().Be("Invalid");
        result.Reason.Should().Be("OutletNotAuthorized");
    }

    [Fact]
    public async Task VerifyAsync_SerialInsteadOfDynamicCode_ReturnsInvalidCodeFormat()
    {
        // A serial number is not token-shaped: extraction fails before any DB lookup.
        _codeService.TryExtractVoucherId("VC-TEST-001", out Arg.Any<Guid>()).Returns(false);

        var result = await _sut.VerifyAsync("VC-TEST-001", _outletId);

        result.Status.Should().Be("Invalid");
        result.Reason.Should().Be("InvalidCodeFormat");
    }

    [Fact]
    public async Task VerifyAsync_ExpiredDynamicCode_ReturnsCodeExpired()
    {
        var detail = new VoucherPlanDetail
        {
            Id = _detailId,
            ParentId = _planId,
            SerialNo = "VC-TEST-003",
            VoucherCodeSecret = "secret-key",
            UsageStatus = UsageStatus.Pending
        };
        _codeService.TryExtractVoucherId("expired-code", out Arg.Any<Guid>())
            .Returns(x => { x[1] = _detailId; return true; });
        _codeService.CheckCode("expired-code", "secret-key", out Arg.Any<Guid>())
            .Returns(x => { x[2] = _detailId; return VoucherCodeCheck.Expired; });
        _detailRepo.GetByIdAsync(_detailId, Arg.Any<CancellationToken>()).Returns(detail);

        var result = await _sut.VerifyAsync("expired-code", _outletId);

        result.Status.Should().Be("Invalid");
        result.Reason.Should().Be("CodeExpired");
    }

    [Fact]
    public async Task VerifyAsync_BadSignature_ReturnsForged()
    {
        var detail = new VoucherPlanDetail
        {
            Id = _detailId,
            ParentId = _planId,
            SerialNo = "VC-TEST-004",
            VoucherCodeSecret = "secret-key",
            UsageStatus = UsageStatus.Pending
        };
        _codeService.TryExtractVoucherId("tampered-code", out Arg.Any<Guid>())
            .Returns(x => { x[1] = _detailId; return true; });
        _codeService.CheckCode("tampered-code", "secret-key", out Arg.Any<Guid>())
            .Returns(x => { x[2] = _detailId; return VoucherCodeCheck.BadSignature; });
        _detailRepo.GetByIdAsync(_detailId, Arg.Any<CancellationToken>()).Returns(detail);

        var result = await _sut.VerifyAsync("tampered-code", _outletId);

        result.Status.Should().Be("Invalid");
        result.Reason.Should().Be("Forged");
    }
}
