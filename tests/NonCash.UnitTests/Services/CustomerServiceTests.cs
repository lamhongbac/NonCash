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
    private readonly IRepository<CustomerAuditLog> _auditLogRepository = Substitute.For<IRepository<CustomerAuditLog>>();
    private readonly CustomerService _sut;

    public CustomerServiceTests()
    {
        _sut = new CustomerService(_customerRepository, _brandCustomerRepository, _auditLogRepository);
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
        // Arrange — Admin path (no brand scope): a globally existing phone is a real conflict.
        var existing = new Customer { Id = Guid.NewGuid(), PhoneNumber = "1234567890", FullName = "Existing" };
        _customerRepository.GetByPhoneNumberAsync("1234567890", Arg.Any<CancellationToken>()).Returns(existing);

        // Act
        var act = () => _sut.CreateAsync("1234567890", "John", null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task CreateAsync_ExistingPhoneSameBrand_ThrowsInvalidOperationException()
    {
        // Arrange — phone exists AND is already mapped to this brand: a true duplicate for the brand.
        var brandId = Guid.NewGuid();
        var existing = new Customer { Id = Guid.NewGuid(), PhoneNumber = "1234567890", FullName = "Existing" };
        _customerRepository.GetByPhoneNumberAsync("1234567890", Arg.Any<CancellationToken>()).Returns(existing);
        _brandCustomerRepository.FindAsync(brandId, existing.Id, Arg.Any<CancellationToken>())
            .Returns(new BrandCustomer { BrandId = brandId, CustomerId = existing.Id });

        // Act
        var act = () => _sut.CreateAsync("1234567890", "John", null, default, brandId, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
        await _brandCustomerRepository.DidNotReceive().EnsureAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<BrandCustomerSource>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ExistingPhoneNoMapping_LinksExistingCustomer()
    {
        // Arrange — phone exists globally but is unknown to this brand: link, never duplicate.
        var brandId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var existing = new Customer { Id = Guid.NewGuid(), PhoneNumber = "1234567890", FullName = "Old Name" };
        _customerRepository.GetByPhoneNumberAsync("1234567890", Arg.Any<CancellationToken>()).Returns(existing);
        _brandCustomerRepository.FindAsync(brandId, existing.Id, Arg.Any<CancellationToken>())
            .Returns((BrandCustomer?)null);

        // Act
        var result = await _sut.CreateAsync("1234567890", "New Name", "new@example.com", default, brandId, createdBy);

        // Assert — CR-11 fill-empty-only: the real existing name is kept, only the empty
        // email is filled; the customer is linked to the brand, never duplicated.
        result.Id.Should().Be(existing.Id);
        result.FullName.Should().Be("Old Name");
        result.Email.Should().Be("new@example.com");
        _customerRepository.Received(1).Update(existing);
        await _customerRepository.DidNotReceive().AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>());
        await _brandCustomerRepository.Received(1).EnsureAsync(
            brandId, existing.Id, BrandCustomerSource.Manual, createdBy, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ExistingPhone_EmailTakenByAnother_ThrowsInvalidOperationException()
    {
        // Arrange — linking must not steal an email owned by a different customer.
        var brandId = Guid.NewGuid();
        var existing = new Customer { Id = Guid.NewGuid(), PhoneNumber = "1234567890", FullName = "Existing" };
        _customerRepository.GetByPhoneNumberAsync("1234567890", Arg.Any<CancellationToken>()).Returns(existing);
        _brandCustomerRepository.FindAsync(brandId, existing.Id, Arg.Any<CancellationToken>())
            .Returns((BrandCustomer?)null);
        _customerRepository.EmailExistsAsync("taken@example.com", existing.Id, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var act = () => _sut.CreateAsync("1234567890", "John", "taken@example.com", default, brandId, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_ReturnsUpdatedCustomer()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var changedBy = Guid.NewGuid();
        var existing = new Customer { Id = customerId, PhoneNumber = "1234567890", FullName = "Old Name" };
        _customerRepository.GetByIdAsync(customerId, Arg.Any<CancellationToken>()).Returns(existing);

        // Act — admin path (no brand scope): full edit, every change audited (CR-14).
        var result = await _sut.UpdateAsync(customerId, "New Name", "new@example.com", default, null, changedBy);

        // Assert
        result.FullName.Should().Be("New Name");
        result.Email.Should().Be("new@example.com");
        await _customerRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLogRepository.Received(1).AddAsync(
            Arg.Is<CustomerAuditLog>(x => x.CustomerId == customerId && x.Field == "FullName"
                && x.OldValue == "Old Name" && x.NewValue == "New Name" && x.ChangedByUserId == changedBy),
            Arg.Any<CancellationToken>());
        await _auditLogRepository.Received(1).AddAsync(
            Arg.Is<CustomerAuditLog>(x => x.CustomerId == customerId && x.Field == "Email"
                && x.OldValue == null && x.NewValue == "new@example.com"),
            Arg.Any<CancellationToken>());
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

        // Assert — CR-10 regression: fills on the no-tracking entity REQUIRE Update().
        // CR-11: the differing name is kept (reported skipped); the empty email is filled.
        result.Created.Should().Be(1);
        result.Updated.Should().Be(1);
        result.Unchanged.Should().Be(0);
        result.Errors.Should().BeEmpty();
        result.Skipped.Should().ContainSingle().Which.Message.Should().Contain("name");
        existing.FullName.Should().Be("Old Name");
        existing.Email.Should().Be("updated@example.com");
        _customerRepository.Received(1).Update(existing);
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

    // ----- CR-2026-09-06-11 fill-empty-only + CR-2026-09-07-14 admin audit -----

    [Fact]
    public async Task CreateAsync_PlaceholderExisting_FillsNameAndEmail()
    {
        // CR-11: a placeholder row (transfer/gifting created FullName = phone) IS fillable.
        var brandId = Guid.NewGuid();
        var existing = new Customer { Id = Guid.NewGuid(), PhoneNumber = "1234567890", FullName = "1234567890" };
        _customerRepository.GetByPhoneNumberAsync("1234567890", Arg.Any<CancellationToken>()).Returns(existing);
        _brandCustomerRepository.FindAsync(brandId, existing.Id, Arg.Any<CancellationToken>())
            .Returns((BrandCustomer?)null);

        var result = await _sut.CreateAsync("1234567890", "Real Name", "real@example.com", default, brandId, null);

        result.FullName.Should().Be("Real Name");
        result.Email.Should().Be("real@example.com");
        _customerRepository.Received(1).Update(existing);
    }

    [Fact]
    public async Task CreateAsync_ExistingWithEmail_KeepsEmailAndSkipsConflictCheck()
    {
        // CR-11: the incoming email would never be written (existing email is set), so a
        // conflict on it is moot — linking succeeds and the existing email stays.
        var brandId = Guid.NewGuid();
        var existing = new Customer { Id = Guid.NewGuid(), PhoneNumber = "1234567890", FullName = "Real Name", Email = "own@example.com" };
        _customerRepository.GetByPhoneNumberAsync("1234567890", Arg.Any<CancellationToken>()).Returns(existing);
        _brandCustomerRepository.FindAsync(brandId, existing.Id, Arg.Any<CancellationToken>())
            .Returns((BrandCustomer?)null);
        _customerRepository.EmailExistsAsync("taken@example.com", existing.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.CreateAsync("1234567890", "Other Name", "taken@example.com", default, brandId, null);

        result.FullName.Should().Be("Real Name");
        result.Email.Should().Be("own@example.com");
        _customerRepository.DidNotReceive().Update(Arg.Any<Customer>());
    }

    [Fact]
    public async Task UpdateAsync_BrandPath_FillsOnlyEmptyFields()
    {
        // CR-11: brand edits never overwrite a real name; they fill empty fields only.
        var brandId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var existing = new Customer { Id = customerId, PhoneNumber = "1234567890", FullName = "Real Name" };
        _brandCustomerRepository.FindAsync(brandId, customerId, Arg.Any<CancellationToken>())
            .Returns(new BrandCustomer { BrandId = brandId, CustomerId = customerId });
        _customerRepository.GetByIdAsync(customerId, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await _sut.UpdateAsync(customerId, "Brand Rewrite", "filled@example.com", default, brandId);

        result.FullName.Should().Be("Real Name");       // kept
        result.Email.Should().Be("filled@example.com"); // filled (was empty)
        await _auditLogRepository.DidNotReceive().AddAsync(Arg.Any<CustomerAuditLog>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_AdminPath_NoChanges_WritesNoAudit()
    {
        // CR-14: identical values are not a change — no audit noise.
        var customerId = Guid.NewGuid();
        var existing = new Customer { Id = customerId, PhoneNumber = "1234567890", FullName = "Same Name", Email = "same@example.com" };
        _customerRepository.GetByIdAsync(customerId, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await _sut.UpdateAsync(customerId, "Same Name", "same@example.com");

        result.FullName.Should().Be("Same Name");
        result.Email.Should().Be("same@example.com");
        await _auditLogRepository.DidNotReceive().AddAsync(Arg.Any<CustomerAuditLog>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertAsync_DifferingValues_AreKeptAndReportedSkipped()
    {
        // CR-11: a row whose name/email differ from existing real values writes nothing
        // and is reported in Skipped (not Updated, not Unchanged).
        var phone = "1112223333";
        var existing = new Customer { Id = Guid.NewGuid(), PhoneNumber = phone, FullName = "Real Name", Email = "real@example.com" };
        _customerRepository.GetByPhoneNumberAsync(phone, Arg.Any<CancellationToken>()).Returns(existing);

        var records = new List<CustomerImportRecord> { new(phone, "Other Name", "other@example.com") };

        var result = await _sut.UpsertAsync(records);

        result.Updated.Should().Be(0);
        result.Unchanged.Should().Be(0);
        result.Skipped.Should().ContainSingle().Which.Message.Should().Contain("name").And.Contain("email");
        existing.FullName.Should().Be("Real Name");
        existing.Email.Should().Be("real@example.com");
        _customerRepository.DidNotReceive().Update(Arg.Any<Customer>());
    }

    [Fact]
    public async Task UpsertAsync_IdenticalRow_CountsUnchanged()
    {
        var phone = "1112223333";
        var existing = new Customer { Id = Guid.NewGuid(), PhoneNumber = phone, FullName = "Same Name", Email = "same@example.com" };
        _customerRepository.GetByPhoneNumberAsync(phone, Arg.Any<CancellationToken>()).Returns(existing);

        var records = new List<CustomerImportRecord> { new(phone, "Same Name", "same@example.com") };

        var result = await _sut.UpsertAsync(records);

        result.Updated.Should().Be(0);
        result.Unchanged.Should().Be(1);
        result.Skipped.Should().BeEmpty();
        _customerRepository.DidNotReceive().Update(Arg.Any<Customer>());
    }

    [Fact]
    public async Task UpsertAsync_PlaceholderRow_PersistsFillsViaUpdate()
    {
        // CR-10 regression: fills onto the no-tracking entity are lost without Update().
        var phone = "1112223333";
        var existing = new Customer { Id = Guid.NewGuid(), PhoneNumber = phone, FullName = phone };
        _customerRepository.GetByPhoneNumberAsync(phone, Arg.Any<CancellationToken>()).Returns(existing);

        var records = new List<CustomerImportRecord> { new(phone, "Real Name", "real@example.com") };

        var result = await _sut.UpsertAsync(records);

        result.Updated.Should().Be(1);
        result.Skipped.Should().BeEmpty();
        existing.FullName.Should().Be("Real Name");
        existing.Email.Should().Be("real@example.com");
        _customerRepository.Received(1).Update(existing);
    }
}
