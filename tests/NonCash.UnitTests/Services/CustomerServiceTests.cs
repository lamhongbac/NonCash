using FluentAssertions;
using NSubstitute;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;

namespace NonCash.UnitTests.Services;

public class CustomerServiceTests
{
    private readonly ICustomerRepository _customerRepository = Substitute.For<ICustomerRepository>();
    private readonly IBrandCustomerRepository _brandCustomerRepository = Substitute.For<IBrandCustomerRepository>();
    private readonly CustomerService _sut;

    public CustomerServiceTests()
    {
        _sut = new CustomerService(_customerRepository, _brandCustomerRepository);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ReturnsCustomer()
    {
        // Arrange
        _customerRepository.PhoneNumberExistsAsync("1234567890", Arg.Any<CancellationToken>()).Returns(false);
        _customerRepository.AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>()).Returns(x => x.Arg<Customer>());

        // Act
        var result = await _sut.CreateAsync("+1 (234) 567-890", "John Doe", "john@example.com");

        // Assert
        result.Should().NotBeNull();
        result.PhoneNumber.Should().Be("1234567890"); // normalized
        result.FullName.Should().Be("John Doe");
        result.Email.Should().Be("john@example.com");
        result.Status.Should().Be(CustomerStatus.Active);
        await _customerRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithEmptyPhone_ThrowsArgumentException()
    {
        // Act
        var act = () => _sut.CreateAsync("", "John", null);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("phoneNumber");
    }

    [Fact]
    public async Task CreateAsync_WithEmptyName_ThrowsArgumentException()
    {
        // Act
        var act = () => _sut.CreateAsync("1234567890", "  ", null);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("fullName");
    }

    [Fact]
    public async Task CreateAsync_WithDuplicatePhone_ThrowsInvalidOperationException()
    {
        // Arrange
        _customerRepository.PhoneNumberExistsAsync("1234567890", Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var act = () => _sut.CreateAsync("1234567890", "John", null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_ReturnsUpdatedCustomer()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var existing = new Customer { Id = customerId, PhoneNumber = "1234567890", FullName = "Old Name" };
        _customerRepository.GetByIdAsync(customerId, Arg.Any<CancellationToken>()).Returns(existing);

        // Act
        var result = await _sut.UpdateAsync(customerId, "New Name", "new@example.com");

        // Assert
        result.FullName.Should().Be("New Name");
        result.Email.Should().Be("new@example.com");
        await _customerRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WithMissingCustomer_ThrowsKeyNotFoundException()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        _customerRepository.GetByIdAsync(customerId, Arg.Any<CancellationToken>()).Returns((Customer?)null);

        // Act
        var act = () => _sut.UpdateAsync(customerId, "New Name", null);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task BlacklistAsync_SetsStatusToBlacklisted()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var existing = new Customer { Id = customerId, PhoneNumber = "1234567890", FullName = "John", Status = CustomerStatus.Active };
        _customerRepository.GetByIdAsync(customerId, Arg.Any<CancellationToken>()).Returns(existing);

        // Act
        var result = await _sut.BlacklistAsync(customerId);

        // Assert
        result.Status.Should().Be(CustomerStatus.Blacklisted);
        await _customerRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnblacklistAsync_SetsStatusToActive()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var existing = new Customer { Id = customerId, PhoneNumber = "1234567890", FullName = "John", Status = CustomerStatus.Blacklisted };
        _customerRepository.GetByIdAsync(customerId, Arg.Any<CancellationToken>()).Returns(existing);

        // Act
        var result = await _sut.UnblacklistAsync(customerId);

        // Assert
        result.Status.Should().Be(CustomerStatus.Active);
    }

    [Fact]
    public async Task IsBlacklisted_ReturnsTrueForBlacklistedCustomer()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var existing = new Customer { Id = customerId, Status = CustomerStatus.Blacklisted };
        _customerRepository.GetByIdAsync(customerId, Arg.Any<CancellationToken>()).Returns(existing);

        // Act
        var result = await _sut.IsBlacklisted(customerId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertAsync_CreatesNewAndUpdatesExisting()
    {
        // Arrange
        var existingPhone = "1112223333";
        var existing = new Customer { Id = Guid.NewGuid(), PhoneNumber = existingPhone, FullName = "Old Name" };
        _customerRepository.GetByPhoneNumberAsync(existingPhone, Arg.Any<CancellationToken>()).Returns(existing);
        _customerRepository.GetByPhoneNumberAsync("4445556666", Arg.Any<CancellationToken>()).Returns((Customer?)null);
        _customerRepository.AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>()).Returns(x => x.Arg<Customer>());

        var records = new List<CustomerImportRecord>
        {
            new(existingPhone, "Updated Name", "updated@example.com"),
            new("4445556666", "New Customer", "new@example.com")
        };

        // Act
        var result = await _sut.UpsertAsync(records);

        // Assert
        result.Created.Should().Be(1);
        result.Updated.Should().Be(1);
        result.Errors.Should().BeEmpty();
        await _customerRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertAsync_SkipsInvalidPhoneNumbers()
    {
        // Arrange
        var records = new List<CustomerImportRecord>
        {
            new("abc", "Bad Phone", null)
        };

        // Act
        var result = await _sut.UpsertAsync(records);

        // Assert
        result.Created.Should().Be(0);
        result.Updated.Should().Be(0);
        result.Errors.Should().ContainSingle().Which.Message.Should().Contain("Invalid phone number");
    }

    [Fact]
    public async Task CreateAsync_WithBrandId_WritesMapping()
    {
        // Arrange
        _customerRepository.PhoneNumberExistsAsync("1234567890", Arg.Any<CancellationToken>()).Returns(false);
        _customerRepository.AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>()).Returns(x => x.Arg<Customer>());
        var brandId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();

        // Act
        var result = await _sut.CreateAsync("1234567890", "John Doe", null, default, brandId, createdBy);

        // Assert
        await _brandCustomerRepository.Received(1).EnsureAsync(
            brandId, result.Id, BrandCustomerSource.Manual, createdBy, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertAsync_Reimport_StillEnsuresMappingForCreatedAndUpdatedRows()
    {
        // Arrange — stateful stub: first lookup misses, second hits the stored customer,
        // so the first import CREATES and the second import UPDATES the same row.
        var brandId = Guid.NewGuid();
        Customer? stored = null;
        _customerRepository.GetByPhoneNumberAsync("4445556666", Arg.Any<CancellationToken>())
            .Returns(_ => stored);
        _customerRepository.AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                stored = ci.Arg<Customer>();
                return stored;
            });
        var records = new List<CustomerImportRecord> { new("4445556666", "Reimported Customer", null) };

        // Act
        await _sut.UpsertAsync(records, default, brandId, null);
        await _sut.UpsertAsync(records, default, brandId, null);

        // Assert — every import pass re-invokes the idempotent EnsureAsync
        // (created branch AND updated branch); dedup itself lives in the repository.
        await _brandCustomerRepository.Received(2).EnsureAsync(
            brandId, Arg.Any<Guid>(), BrandCustomerSource.Import, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BlockAsync_Then_Unblock_DelegatesToRepository()
    {
        // Arrange
        var brandId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        // Act
        await _sut.BlockAsync(brandId, customerId);
        await _sut.UnblockAsync(brandId, customerId);

        // Assert
        await _brandCustomerRepository.Received(1).SetBlockedAsync(brandId, customerId, true, Arg.Any<CancellationToken>());
        await _brandCustomerRepository.Received(1).SetBlockedAsync(brandId, customerId, false, Arg.Any<CancellationToken>());
    }
}
