using FluentAssertions;
using NSubstitute;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;

namespace NonCash.UnitTests.Services;

public class BrandServiceTests
{
    private readonly IBrandRepository _brandRepository = Substitute.For<IBrandRepository>();
    private readonly BrandService _sut;

    public BrandServiceTests()
    {
        _sut = new BrandService(_brandRepository);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ReturnsBrand()
    {
        // Arrange
        _brandRepository.TaxCodeExistsAsync("TAX123", Arg.Any<CancellationToken>()).Returns(false);
        _brandRepository.AddAsync(Arg.Any<Brand>(), Arg.Any<CancellationToken>()).Returns(x => x.Arg<Brand>());

        // Act
        var result = await _sut.CreateAsync("Test Brand", "TAX123", "test@example.com");

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Test Brand");
        result.TaxCode.Should().Be("TAX123");
        result.ContactEmail.Should().Be("test@example.com");
        result.Status.Should().Be(BrandStatus.Active);
        await _brandRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithEmptyName_ThrowsArgumentException()
    {
        // Act
        var act = () => _sut.CreateAsync("", "TAX123", null);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateTaxCode_ThrowsInvalidOperationException()
    {
        // Arrange
        _brandRepository.TaxCodeExistsAsync("TAX123", Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var act = () => _sut.CreateAsync("Test Brand", "TAX123", null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*tax code*'TAX123'*already exists*");
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_ReturnsUpdatedBrand()
    {
        // Arrange
        var brandId = Guid.NewGuid();
        var existing = new Brand
        {
            Id = brandId,
            Name = "Old Name",
            TaxCode = "TAX123",
            ContactEmail = "old@example.com",
            Status = BrandStatus.Active
        };
        _brandRepository.GetByIdAsync(brandId, Arg.Any<CancellationToken>()).Returns(existing);

        // Act
        var result = await _sut.UpdateAsync(brandId, "New Name", "new@example.com", BrandStatus.Suspended);

        // Assert
        result.Name.Should().Be("New Name");
        result.ContactEmail.Should().Be("new@example.com");
        result.Status.Should().Be(BrandStatus.Suspended);
        await _brandRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WithMissingBrand_ThrowsKeyNotFoundException()
    {
        // Arrange
        var brandId = Guid.NewGuid();
        _brandRepository.GetByIdAsync(brandId, Arg.Any<CancellationToken>()).Returns((Brand?)null);

        // Act
        var act = () => _sut.UpdateAsync(brandId, "New Name", null, BrandStatus.Active);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var brandId = Guid.NewGuid();
        var existing = new Brand { Id = brandId, Name = "Old", TaxCode = "TAX123" };
        _brandRepository.GetByIdAsync(brandId, Arg.Any<CancellationToken>()).Returns(existing);

        // Act
        var act = () => _sut.UpdateAsync(brandId, "   ", null, BrandStatus.Active);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("name");
    }
}
