using FluentAssertions;
using NSubstitute;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;

namespace NonCash.UnitTests.Services;

public class OutletServiceTests
{
    private readonly IOutletRepository _outletRepository = Substitute.For<IOutletRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly OutletService _sut;
    private readonly Guid _brandId = Guid.NewGuid();

    public OutletServiceTests()
    {
        _currentUserService.GetCurrentBrandId().Returns(_brandId);
        _sut = new OutletService(_outletRepository, _currentUserService);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ReturnsOutlet()
    {
        // Arrange
        _outletRepository.AddAsync(Arg.Any<Outlet>(), Arg.Any<CancellationToken>()).Returns(x => x.Arg<Outlet>());

        // Act
        var result = await _sut.CreateAsync("Test Outlet", "123 Main St");

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Test Outlet");
        result.Address.Should().Be("123 Main St");
        result.BrandId.Should().Be(_brandId);
        result.Status.Should().Be(OutletStatus.Active);
        result.ApiKeyPrefix.Should().NotBeNullOrEmpty();
        result.ApiKeyPrefix.Length.Should().Be(8);
        await _outletRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithEmptyName_ThrowsArgumentException()
    {
        // Act
        var act = () => _sut.CreateAsync("  ", null);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public async Task CreateAsync_WithoutBrandId_ThrowsInvalidOperationException()
    {
        // Arrange
        _currentUserService.GetCurrentBrandId().Returns((Guid?)null);

        // Act
        var act = () => _sut.CreateAsync("Test", null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*BrandID*not available*");
    }

    [Fact]
    public async Task ListByBrandAsync_ReturnsScopedOutlets()
    {
        // Arrange
        var outlets = new List<Outlet>
        {
            new() { Id = Guid.NewGuid(), BrandId = _brandId, Name = "Outlet A", Status = OutletStatus.Active },
            new() { Id = Guid.NewGuid(), BrandId = _brandId, Name = "Outlet B", Status = OutletStatus.Closed }
        };
        _outletRepository.ListByBrandAsync(_brandId, Arg.Any<CancellationToken>()).Returns(outlets);

        // Act
        var (items, totalCount) = await _sut.ListByBrandAsync(null, null, 1, 10);

        // Assert
        totalCount.Should().Be(2);
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListByBrandAsync_FiltersByName()
    {
        // Arrange
        var outlets = new List<Outlet>
        {
            new() { Id = Guid.NewGuid(), BrandId = _brandId, Name = "Alpha Outlet", Status = OutletStatus.Active },
            new() { Id = Guid.NewGuid(), BrandId = _brandId, Name = "Beta Outlet", Status = OutletStatus.Active }
        };
        _outletRepository.ListByBrandAsync(_brandId, Arg.Any<CancellationToken>()).Returns(outlets);

        // Act
        var (items, totalCount) = await _sut.ListByBrandAsync("Alpha", null, 1, 10);

        // Assert
        totalCount.Should().Be(1);
        items.Single().Name.Should().Be("Alpha Outlet");
    }

    [Fact]
    public async Task GetByIdAsync_WithMatchingBrand_ReturnsOutlet()
    {
        // Arrange
        var outletId = Guid.NewGuid();
        var outlet = new Outlet { Id = outletId, BrandId = _brandId, Name = "My Outlet" };
        _outletRepository.GetByIdAsync(outletId, Arg.Any<CancellationToken>()).Returns(outlet);

        // Act
        var result = await _sut.GetByIdAsync(outletId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(outletId);
    }

    [Fact]
    public async Task GetByIdAsync_WithOtherBrand_ReturnsNull()
    {
        // Arrange
        var outletId = Guid.NewGuid();
        var outlet = new Outlet { Id = outletId, BrandId = Guid.NewGuid(), Name = "Other Outlet" };
        _outletRepository.GetByIdAsync(outletId, Arg.Any<CancellationToken>()).Returns(outlet);

        // Act
        var result = await _sut.GetByIdAsync(outletId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_ReturnsUpdatedOutlet()
    {
        // Arrange
        var outletId = Guid.NewGuid();
        var existing = new Outlet { Id = outletId, BrandId = _brandId, Name = "Old Name", Address = "Old Addr" };
        _outletRepository.GetByIdAsync(outletId, Arg.Any<CancellationToken>()).Returns(existing);

        // Act
        var result = await _sut.UpdateAsync(outletId, "New Name", "New Addr");

        // Assert
        result.Name.Should().Be("New Name");
        result.Address.Should().Be("New Addr");
        await _outletRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WithOtherBrand_ThrowsKeyNotFoundException()
    {
        // Arrange
        var outletId = Guid.NewGuid();
        var existing = new Outlet { Id = outletId, BrandId = Guid.NewGuid(), Name = "Other Outlet" };
        _outletRepository.GetByIdAsync(outletId, Arg.Any<CancellationToken>()).Returns(existing);

        // Act
        var act = () => _sut.UpdateAsync(outletId, "New Name", null);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task CloseAsync_SetsStatusToClosed()
    {
        // Arrange
        var outletId = Guid.NewGuid();
        var existing = new Outlet { Id = outletId, BrandId = _brandId, Name = "My Outlet", Status = OutletStatus.Active };
        _outletRepository.GetByIdAsync(outletId, Arg.Any<CancellationToken>()).Returns(existing);

        // Act
        var result = await _sut.CloseAsync(outletId);

        // Assert
        result.Status.Should().Be(OutletStatus.Closed);
        await _outletRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
