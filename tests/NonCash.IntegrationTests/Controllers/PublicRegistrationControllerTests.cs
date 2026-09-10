using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NonCash.API.Controllers;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;
using NonCash.Infrastructure.Services;

namespace NonCash.IntegrationTests.Controllers;

public class PublicRegistrationControllerTests
{
    private readonly IRegistrationService _registrationService;
    private readonly FakeBusinessRegistrationRequestRepository _requestRepository;

    public PublicRegistrationControllerTests()
    {
        var brandRepository = new FakeBrandRepository();
        var userAccountRepository = new FakeUserAccountRepository();
        _requestRepository = new FakeBusinessRegistrationRequestRepository();
        var notificationService = new FileNotificationService();
        var authService = new AuthService(userAccountRepository, new FakeMemberAccountRepository(), new FakeJwtTokenService(), notificationService, new FakeCustomerRepository(), new FakeOutletRepository(), new FakeUserOutletRepository(), Fixtures.TestJwtConfig.Create());

        _registrationService = new RegistrationService(
            new FakeBusinessRepository(),
            brandRepository,
            userAccountRepository,
            _requestRepository,
            authService,
            notificationService);
    }

    [Fact]
    public async Task SubmitAsync_WithFirstBrandDeclaration_CreatesRequestOnly()
    {
        // Arrange
        var request = new RegistrationRequestDto(
            "Galaxy Restaurant",
            "TXGL001",
            "test@example.com",
            "0909000001",
            "123 Main St",
            "John Doe",
            "Galaxy Brand",
            "galaxymanager",
            "Password123!");

        // Act
        var result = await _registrationService.SubmitAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.RequestId.Should().NotBeNull();
        result.BusinessId.Should().BeNull();
        result.BrandId.Should().BeNull();
    }

    [Fact]
    public async Task SubmitAsync_WithoutFirstBrandDeclaration_CreatesRequestOnly()
    {
        // Arrange
        var request = new RegistrationRequestDto(
            "Nebula Restaurant",
            "TXNEB001",
            "nebula@example.com",
            "0909000002",
            "456 Main St",
            "Jane Doe");

        // Act
        var result = await _registrationService.SubmitAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.RequestId.Should().NotBeNull();
        result.BusinessId.Should().BeNull();
        result.BrandId.Should().BeNull();
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
            "Galaxy Brand",
            "galaxymanager1",
            "Password123!");

        var request2 = new RegistrationRequestDto(
            "Galaxy Duplicate",
            "TXGL001",
            "test2@example.com",
            "0909000002",
            "456 Main St",
            "Jane Doe",
            "Galaxy Duplicate Brand",
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
    public async Task ReviewAsync_Approve_WithFirstBrandDeclaration_CreatesBusinessBrandAndUser()
    {
        // Arrange
        var request = new RegistrationRequestDto(
            "Galaxy Restaurant",
            "TXGL002",
            "test@example.com",
            "0909000001",
            "123 Main St",
            "John Doe",
            "Galaxy Brand",
            "galaxymanager3",
            "Password123!");

        var submitResult = await _registrationService.SubmitAsync(request);
        var reviewerId = Guid.NewGuid();

        // Simulate contract signed (set directly on the stored request for this unit-level test).
        var storedRequest = await _requestRepository.GetByIdAsync(submitResult.RequestId!.Value);
        storedRequest!.ContractStatus = ContractStatus.Signed;
        storedRequest.WelcomePolicyTemplateId = Guid.NewGuid();

        // Act
        var reviewResult = await _registrationService.ReviewAsync(submitResult.RequestId!.Value, reviewerId, true, null);

        // Assert
        reviewResult.Success.Should().BeTrue();

        var status = await _registrationService.GetStatusAsync(submitResult.RequestId.Value);
        status!.Status.Should().Be(RegistrationStatus.Approved);

        storedRequest = await _requestRepository.GetByIdAsync(submitResult.RequestId.Value);
        storedRequest!.BusinessId.Should().NotBeNull();
        storedRequest.BrandId.Should().NotBeNull();
        storedRequest.SubmittedByUserId.Should().NotBeNull();
    }

    [Fact]
    public async Task ReviewAsync_Approve_WithoutFirstBrandDeclaration_CreatesBusinessOnly()
    {
        // Arrange
        var request = new RegistrationRequestDto(
            "Nebula Restaurant",
            "TXNEB002",
            "nebula@example.com",
            "0909000002",
            "456 Main St",
            "Jane Doe");

        var submitResult = await _registrationService.SubmitAsync(request);
        var reviewerId = Guid.NewGuid();

        var storedRequest = await _requestRepository.GetByIdAsync(submitResult.RequestId!.Value);
        storedRequest!.ContractStatus = ContractStatus.Signed;
        storedRequest.WelcomePolicyTemplateId = Guid.NewGuid();

        // Act
        var reviewResult = await _registrationService.ReviewAsync(submitResult.RequestId!.Value, reviewerId, true, null);

        // Assert
        reviewResult.Success.Should().BeTrue();

        storedRequest = await _requestRepository.GetByIdAsync(submitResult.RequestId.Value);
        storedRequest!.BusinessId.Should().NotBeNull();
        storedRequest.BrandId.Should().BeNull();
        storedRequest.SubmittedByUserId.Should().BeNull();
    }

    [Fact]
    public async Task Register_BlacklistedCustomer_Returns403()
    {
        // Matrix row 11 (gap #7): a platform-blacklisted customer cannot self-register
        // a member account. Generic message — no blacklist detail leak.
        // Arrange — an existing customer with the registered phone is blacklisted
        var phone = "0909333333";
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            PhoneNumber = phone,
            FullName = "Banned Customer",
            Email = "banned@example.com",
            Status = CustomerStatus.Blacklisted
        };
        var customerRepository = new FakeCustomerRepository(new[] { customer });
        var memberRepository = new FakeMemberAccountRepository();
        var authService = new AuthService(
            new FakeUserAccountRepository(),
            memberRepository,
            new FakeJwtTokenService(),
            new FileNotificationService(),
            customerRepository,
            new FakeOutletRepository(),
            new FakeUserOutletRepository(),
            Fixtures.TestJwtConfig.Create());
        var customerService = new CustomerService(customerRepository, new FakeBrandCustomerRepository(), new FakeCustomerAuditLogRepository());
        var controller = new MemberRegistrationController(customerService, customerRepository, memberRepository, authService, new Fixtures.StubVoucherTransferService());

        var request = new MemberRegisterRequest("Password@123", "Banned Customer", phone, "banned@example.com");

        // Act
        var result = await controller.Register(request, CancellationToken.None);

        // Assert
        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        memberRepository.Added.Should().BeEmpty();
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

    private class FakeBusinessRegistrationRequestRepository : IBusinessRegistrationRequestRepository
    {
        private readonly List<BusinessRegistrationRequest> _requests = new();

        public Task<BusinessRegistrationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_requests.FirstOrDefault(r => r.Id == id));
        public Task<IEnumerable<BusinessRegistrationRequest>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<BusinessRegistrationRequest>>(_requests);
        public Task<IEnumerable<BusinessRegistrationRequest>> FindAsync(Expression<Func<BusinessRegistrationRequest, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<BusinessRegistrationRequest>>(_requests.AsQueryable().Where(predicate));
        public Task<int> CountAsync(Expression<Func<BusinessRegistrationRequest, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult(_requests.AsQueryable().Count(predicate));
        public Task<BusinessRegistrationRequest?> GetByBrandIdAsync(Guid brandId, CancellationToken cancellationToken = default) => Task.FromResult(_requests.FirstOrDefault(r => r.BrandId == brandId));
        public Task<BusinessRegistrationRequest> AddAsync(BusinessRegistrationRequest entity, CancellationToken cancellationToken = default)
        {
            entity.Id = Guid.NewGuid();
            _requests.Add(entity);
            return Task.FromResult(entity);
        }
        public void Update(BusinessRegistrationRequest entity)
        {
            var existing = _requests.FirstOrDefault(r => r.Id == entity.Id);
            if (existing != null)
            {
                _requests.Remove(existing);
                _requests.Add(entity);
            }
        }
        public void Delete(BusinessRegistrationRequest entity) { }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class FakeJwtTokenService : IJwtTokenService
    {
        public string GenerateToken(UserAccount user) => "fake-token";
        public string GenerateToken(UserAccount user, Guid outletId) => "fake-staff-token";
        public string GenerateToken(MemberAccount member) => "fake-member-token";
        public DateTime GetTokenExpiry() => DateTime.UtcNow.AddHours(1);
        public string GenerateMagicLinkToken(Guid memberAccountId) => "fake-magic-link-token";
        public string GenerateSignInLinkToken(Guid memberAccountId) => "fake-sign-in-link-token";
        public Guid? ValidateMagicLinkToken(string token) => null;
    }

    private class FakeMemberAccountRepository : IMemberAccountRepository
    {
        public List<MemberAccount> Added { get; } = new();

        public Task<MemberAccount?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult<MemberAccount?>(null);
        public Task<MemberAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<MemberAccount?>(null);
        public Task<IEnumerable<MemberAccount>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<MemberAccount>>(new List<MemberAccount>());
        public Task<IEnumerable<MemberAccount>> FindAsync(Expression<Func<MemberAccount, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<MemberAccount>>(new List<MemberAccount>());
        public Task<int> CountAsync(Expression<Func<MemberAccount, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<MemberAccount> AddAsync(MemberAccount entity, CancellationToken cancellationToken = default)
        {
            entity.Id = Guid.NewGuid();
            Added.Add(entity);
            return Task.FromResult(entity);
        }
        public void Update(MemberAccount entity) { }
        public void Delete(MemberAccount entity) { }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    // Stateful stub: tests seed customers (e.g. a blacklisted one); the rest is inert.
    private class FakeCustomerRepository : ICustomerRepository
    {
        private readonly List<Customer> _customers;

        public FakeCustomerRepository(IEnumerable<Customer>? customers = null)
            => _customers = customers?.ToList() ?? new List<Customer>();

        public Task<Customer?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default) => Task.FromResult(_customers.FirstOrDefault(c => c.PhoneNumber == phoneNumber));
        public Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult(_customers.FirstOrDefault(c => c.Email == email));
        public Task<bool> PhoneNumberExistsAsync(string phoneNumber, CancellationToken cancellationToken = default) => Task.FromResult(_customers.Any(c => c.PhoneNumber == phoneNumber));
        public Task<bool> EmailExistsAsync(string email, Guid? excludeCustomerId = null, CancellationToken cancellationToken = default) => Task.FromResult(_customers.Any(c => c.Email == email && c.Id != excludeCustomerId));
        public Task<IEnumerable<Customer>> SearchAsync(string? search, CustomerStatus? status, CancellationToken cancellationToken = default, Guid? brandId = null) => Task.FromResult<IEnumerable<Customer>>(_customers);
        public Task<int> CountAsync(string? search, CustomerStatus? status, CancellationToken cancellationToken = default, Guid? brandId = null) => Task.FromResult(_customers.Count);
        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_customers.FirstOrDefault(c => c.Id == id));
        public Task<IEnumerable<Customer>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<Customer>>(_customers);
        public Task<IEnumerable<Customer>> FindAsync(Expression<Func<Customer, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<Customer>>(_customers.AsQueryable().Where(predicate).ToList());
        public Task<int> CountAsync(Expression<Func<Customer, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult(_customers.AsQueryable().Count(predicate));
        public Task<Customer> AddAsync(Customer entity, CancellationToken cancellationToken = default)
        {
            entity.Id = Guid.NewGuid();
            _customers.Add(entity);
            return Task.FromResult(entity);
        }
        public void Update(Customer entity) { }
        public void Delete(Customer entity) { }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    // The register path never touches brand mappings (self-registered customers are
    // platform assets until their first brand interaction — D2); inert ctor stub.
    private class FakeBrandCustomerRepository : IBrandCustomerRepository
    {
        public Task EnsureAsync(Guid brandId, Guid customerId, BrandCustomerSource source, Guid? createdBy = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<BrandCustomer?> FindAsync(Guid brandId, Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult<BrandCustomer?>(null);
        public Task<IReadOnlyList<BrandCustomer>> GetForBrandAsync(Guid brandId, IEnumerable<Guid> customerIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<BrandCustomer>>(new List<BrandCustomer>());
        public Task<bool> IsBlockedAsync(Guid brandId, Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task SetBlockedAsync(Guid brandId, Guid customerId, bool blocked, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    // Self-registration never audits (no admin edits); inert ctor stub for CR-14.
    private class FakeCustomerAuditLogRepository : IRepository<CustomerAuditLog>
    {
        public Task<CustomerAuditLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<CustomerAuditLog?>(null);
        public Task<IEnumerable<CustomerAuditLog>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<CustomerAuditLog>>(new List<CustomerAuditLog>());
        public Task<IEnumerable<CustomerAuditLog>> FindAsync(Expression<Func<CustomerAuditLog, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<CustomerAuditLog>>(new List<CustomerAuditLog>());
        public Task<int> CountAsync(Expression<Func<CustomerAuditLog, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<CustomerAuditLog> AddAsync(CustomerAuditLog entity, CancellationToken cancellationToken = default) => Task.FromResult(entity);
        public void Update(CustomerAuditLog entity) { }
        public void Delete(CustomerAuditLog entity) { }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class FakeOutletRepository : IOutletRepository
    {
        public Task<Outlet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Outlet?>(null);
        public Task<IEnumerable<Outlet>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<Outlet>>(new List<Outlet>());
        public Task<IEnumerable<Outlet>> FindAsync(Expression<Func<Outlet, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<Outlet>>(new List<Outlet>());
        public Task<int> CountAsync(Expression<Func<Outlet, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<Outlet> AddAsync(Outlet entity, CancellationToken cancellationToken = default) => Task.FromResult(entity);
        public void Update(Outlet entity) { }
        public void Delete(Outlet entity) { }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<Outlet>> ListByBrandAsync(Guid brandId, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<Outlet>>(new List<Outlet>());
        public Task<int> CountByBrandAsync(Guid brandId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<Outlet?> GetByCodeAsync(Guid brandId, string code, CancellationToken cancellationToken = default) => Task.FromResult<Outlet?>(null);
    }

    private class FakeUserOutletRepository : IUserOutletRepository
    {
        public Task<UserOutlet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<UserOutlet?>(null);
        public Task<IEnumerable<UserOutlet>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<UserOutlet>>(new List<UserOutlet>());
        public Task<IEnumerable<UserOutlet>> FindAsync(Expression<Func<UserOutlet, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<UserOutlet>>(new List<UserOutlet>());
        public Task<int> CountAsync(Expression<Func<UserOutlet, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<UserOutlet> AddAsync(UserOutlet entity, CancellationToken cancellationToken = default) => Task.FromResult(entity);
        public void Update(UserOutlet entity) { }
        public void Delete(UserOutlet entity) { }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<Outlet>> GetOutletsForUserAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<Outlet>>(new List<Outlet>());
        public Task<IEnumerable<UserAccount>> GetUsersForOutletAsync(Guid outletId, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<UserAccount>>(new List<UserAccount>());
        public Task ReplaceAssignmentsAsync(Guid userId, Guid brandId, IEnumerable<Guid> outletIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
