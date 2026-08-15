using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;
using NonCash.Infrastructure.Services;

namespace NonCash.IntegrationTests.Controllers;

public class PublicRegistrationControllerTests
{
    private readonly IRegistrationService _registrationService;

    public PublicRegistrationControllerTests()
    {
        var brandRepository = new FakeBrandRepository();
        var userAccountRepository = new FakeUserAccountRepository();
        var requestRepository = new FakeBrandRegistrationRequestRepository();
        var notificationService = new ConsoleNotificationService();
        var authService = new AuthService(userAccountRepository, new FakeMemberAccountRepository(), new FakeJwtTokenService(), notificationService);

        _registrationService = new RegistrationService(
            new FakeBusinessRepository(),
            brandRepository,
            userAccountRepository,
            requestRepository,
            authService,
            notificationService);
    }

    [Fact]
    public async Task SubmitAsync_WithValidData_CreatesBrandAndUser()
    {
        // Arrange
        var request = new RegistrationRequestDto(
            "Galaxy Restaurant",
            "TXGL001",
            "test@example.com",
            "0909000001",
            "123 Main St",
            "John Doe",
            "galaxymanager",
            "Password123!");

        // Act
        var result = await _registrationService.SubmitAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.BusinessId.Should().NotBeNull();
        result.BrandId.Should().NotBeNull();
        result.RequestId.Should().NotBeNull();
    }

    [Fact]
    public async Task SubmitAsync_WithDuplicateTaxCode_ReturnsError()
    {
        // Arrange
        var request1 = new RegistrationRequestDto(
            "Galaxy Restaurant",
            "TXGL001",
            "test@example.com",
            "0909000001",
            "123 Main St",
            "John Doe",
            "galaxymanager1",
            "Password123!");

        var request2 = new RegistrationRequestDto(
            "Galaxy Duplicate",
            "TXGL001",
            "test2@example.com",
            "0909000002",
            "456 Main St",
            "Jane Doe",
            "galaxymanager2",
            "Password123!");

        await _registrationService.SubmitAsync(request1);

        // Act
        var result = await _registrationService.SubmitAsync(request2);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("DuplicateTaxCode");
    }

    [Fact]
    public async Task ReviewAsync_Approve_ActivatesBrandAndUser()
    {
        // Arrange
        var request = new RegistrationRequestDto(
            "Galaxy Restaurant",
            "TXGL002",
            "test@example.com",
            "0909000001",
            "123 Main St",
            "John Doe",
            "galaxymanager3",
            "Password123!");

        var submitResult = await _registrationService.SubmitAsync(request);
        var reviewerId = Guid.NewGuid();

        // Act
        var reviewResult = await _registrationService.ReviewAsync(submitResult.RequestId!.Value, reviewerId, true, null);

        // Assert
        reviewResult.Success.Should().BeTrue();

        var status = await _registrationService.GetStatusAsync(submitResult.RequestId.Value);
        status!.Status.Should().Be(RegistrationStatus.Approved);
    }

    private class FakeBusinessRepository : IBusinessRepository
    {
        private readonly List<Business> _businesses = new();

        public Task<Business?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_businesses.FirstOrDefault(b => b.Id == id));
        public Task<IEnumerable<Business>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<Business>>(_businesses);
        public Task<IEnumerable<Business>> FindAsync(Expression<Func<Business, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<Business>>(_businesses.AsQueryable().Where(predicate));
        public Task<int> CountAsync(Expression<Func<Business, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult(_businesses.AsQueryable().Count(predicate));
        public Task<Business> AddAsync(Business entity, CancellationToken cancellationToken = default)
        {
            entity.Id = Guid.NewGuid();
            _businesses.Add(entity);
            return Task.FromResult(entity);
        }
        public void Update(Business entity) { }
        public void Delete(Business entity) { }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Business?> GetByTaxCodeAsync(string taxCode, CancellationToken cancellationToken = default) => Task.FromResult(_businesses.FirstOrDefault(b => b.TaxCode == taxCode));
        public Task<bool> TaxCodeExistsAsync(string taxCode, CancellationToken cancellationToken = default) => Task.FromResult(_businesses.Any(b => b.TaxCode == taxCode));
    }

    private class FakeBrandRepository : IBrandRepository
    {
        private readonly List<Brand> _brands = new();

        public Task<Brand?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_brands.FirstOrDefault(b => b.Id == id));
        public Task<IEnumerable<Brand>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<Brand>>(_brands);
        public Task<IEnumerable<Brand>> FindAsync(Expression<Func<Brand, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<Brand>>(_brands.AsQueryable().Where(predicate));
        public Task<int> CountAsync(Expression<Func<Brand, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult(_brands.AsQueryable().Count(predicate));
        public Task<Brand> AddAsync(Brand entity, CancellationToken cancellationToken = default)
        {
            entity.Id = Guid.NewGuid();
            _brands.Add(entity);
            return Task.FromResult(entity);
        }
        public void Update(Brand entity) { }
        public void Delete(Brand entity) { }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Brand?> GetByTaxCodeAsync(string taxCode, CancellationToken cancellationToken = default) => Task.FromResult(_brands.FirstOrDefault(b => b.TaxCode == taxCode));
        public Task<bool> TaxCodeExistsAsync(string taxCode, CancellationToken cancellationToken = default) => Task.FromResult(_brands.Any(b => b.TaxCode == taxCode));
    }

    private class FakeUserAccountRepository : IUserAccountRepository
    {
        private readonly List<UserAccount> _users = new();

        public Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_users.FirstOrDefault(u => u.Id == id));
        public Task<IEnumerable<UserAccount>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<UserAccount>>(_users);
        public Task<IEnumerable<UserAccount>> FindAsync(Expression<Func<UserAccount, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<UserAccount>>(_users.AsQueryable().Where(predicate));
        public Task<int> CountAsync(Expression<Func<UserAccount, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult(_users.AsQueryable().Count(predicate));
        public Task<UserAccount> AddAsync(UserAccount entity, CancellationToken cancellationToken = default)
        {
            entity.Id = Guid.NewGuid();
            _users.Add(entity);
            return Task.FromResult(entity);
        }
        public void Update(UserAccount entity) { }
        public void Delete(UserAccount entity) { }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<UserAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) => Task.FromResult(_users.FirstOrDefault(u => u.Username.ToLower() == username.ToLower()));
        public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default) => Task.FromResult(_users.Any(u => u.Username.ToLower() == username.ToLower()));
        public Task<IEnumerable<UserAccount>> ListByBrandAsync(Guid brandId, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<UserAccount>>(_users.Where(u => u.BrandId == brandId));
    }

    private class FakeBrandRegistrationRequestRepository : IBrandRegistrationRequestRepository
    {
        private readonly List<BrandRegistrationRequest> _requests = new();

        public Task<BrandRegistrationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_requests.FirstOrDefault(r => r.Id == id));
        public Task<IEnumerable<BrandRegistrationRequest>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<BrandRegistrationRequest>>(_requests);
        public Task<IEnumerable<BrandRegistrationRequest>> FindAsync(Expression<Func<BrandRegistrationRequest, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<BrandRegistrationRequest>>(_requests.AsQueryable().Where(predicate));
        public Task<int> CountAsync(Expression<Func<BrandRegistrationRequest, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult(_requests.AsQueryable().Count(predicate));
        public Task<BrandRegistrationRequest?> GetByBrandIdAsync(Guid brandId, CancellationToken cancellationToken = default) => Task.FromResult(_requests.FirstOrDefault(r => r.BrandId == brandId));
        public Task<BrandRegistrationRequest> AddAsync(BrandRegistrationRequest entity, CancellationToken cancellationToken = default)
        {
            entity.Id = Guid.NewGuid();
            _requests.Add(entity);
            return Task.FromResult(entity);
        }
        public void Update(BrandRegistrationRequest entity) { }
        public void Delete(BrandRegistrationRequest entity) { }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class FakeJwtTokenService : IJwtTokenService
    {
        public string GenerateToken(UserAccount user) => "fake-token";
        public string GenerateToken(MemberAccount member) => "fake-member-token";
        public DateTime GetTokenExpiry() => DateTime.UtcNow.AddHours(1);
    }

    private class FakeMemberAccountRepository : IMemberAccountRepository
    {
        public Task<MemberAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) => Task.FromResult<MemberAccount?>(null);
        public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<MemberAccount?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult<MemberAccount?>(null);
        public Task<MemberAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<MemberAccount?>(null);
        public Task<IEnumerable<MemberAccount>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<MemberAccount>>(new List<MemberAccount>());
        public Task<IEnumerable<MemberAccount>> FindAsync(Expression<Func<MemberAccount, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<MemberAccount>>(new List<MemberAccount>());
        public Task<int> CountAsync(Expression<Func<MemberAccount, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<MemberAccount> AddAsync(MemberAccount entity, CancellationToken cancellationToken = default) => Task.FromResult(entity);
        public void Update(MemberAccount entity) { }
        public void Delete(MemberAccount entity) { }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
