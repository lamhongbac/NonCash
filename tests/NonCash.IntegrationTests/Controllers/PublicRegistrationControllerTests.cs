using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NonCash.API.Controllers;
using NonCash.API.DTOs;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;
using NonCash.Infrastructure.Data;
using NonCash.Infrastructure.Repositories;
using NonCash.Infrastructure.Services;

namespace NonCash.IntegrationTests.Controllers;

public class PublicRegistrationControllerTests
{
    private readonly ApplicationDbContext _context;
    private readonly PublicRegistrationController _controller;

    public PublicRegistrationControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);

        var brandRepo = new BrandRepository(_context);
        var userRepo = new UserAccountRepository(_context);
        var memberRepo = new FakeMemberAccountRepository();
        var requestRepo = new BrandRegistrationRequestRepository(_context);
        var authService = new AuthService(userRepo, memberRepo, new FakeJwtTokenService());
        var notificationService = new ConsoleNotificationService();

        var registrationService = new RegistrationService(
            brandRepo, userRepo, requestRepo, authService, notificationService);

        _controller = new PublicRegistrationController(registrationService);
    }

    private static BusinessRegistrationRequest CreateValidRequest(string? taxCode = null, string? username = null)
    {
        return new BusinessRegistrationRequest(
            "Test Company",
            taxCode ?? Guid.NewGuid().ToString("N")[..10],
            "contact@test.com",
            "1234567890",
            "123 Test Street",
            "John Doe",
            username ?? $"user_{Guid.NewGuid():N}",
            "Password@123"
        );
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var request = CreateValidRequest();

        // Act
        var result = await _controller.Register(request, CancellationToken.None);

        // Assert
        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        okResult.Should().NotBeNull();
        var response = okResult!.Value as BusinessRegistrationResponse;
        response.Should().NotBeNull();
        response!.Status.Should().Be("Submitted");
        response.RequestId.Should().NotBeEmpty();
        response.BrandId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Register_CreatesBrandWithPendingActivationStatus()
    {
        // Arrange
        var request = CreateValidRequest();

        // Act
        var result = await _controller.Register(request, CancellationToken.None);
        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        var response = okResult!.Value as BusinessRegistrationResponse;

        // Assert
        var brand = await _context.Brands.FindAsync(response!.BrandId);
        brand.Should().NotBeNull();
        brand!.Status.Should().Be(BrandStatus.PendingActivation);
        brand.TaxCode.Should().Be(request.TaxCode);
    }

    [Fact]
    public async Task Register_CreatesUserWithPendingActivationStatus()
    {
        // Arrange
        var request = CreateValidRequest();

        // Act
        var result = await _controller.Register(request, CancellationToken.None);
        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        var response = okResult!.Value as BusinessRegistrationResponse;

        // Assert
        var user = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Username == request.Username.ToLowerInvariant());
        user.Should().NotBeNull();
        user!.Status.Should().Be(UserStatus.PendingActivation);
        user.Role.Should().Be(UserRole.BrandManager);
        user.BrandId.Should().Be(response!.BrandId);
    }

    [Fact]
    public async Task Register_CreatesRegistrationRequest()
    {
        // Arrange
        var request = CreateValidRequest();

        // Act
        var result = await _controller.Register(request, CancellationToken.None);
        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        var response = okResult!.Value as BusinessRegistrationResponse;

        // Assert
        var regRequest = await _context.BrandRegistrationRequests.FindAsync(response!.RequestId);
        regRequest.Should().NotBeNull();
        regRequest!.Status.Should().Be(RegistrationStatus.Submitted);
        regRequest.BrandId.Should().Be(response.BrandId);
    }

    [Fact]
    public async Task Register_WithDuplicateTaxCode_ReturnsBadRequest()
    {
        // Arrange
        var request1 = CreateValidRequest("DUPLICATE-TAX-001");
        var request2 = CreateValidRequest("DUPLICATE-TAX-001");

        await _controller.Register(request1, CancellationToken.None);

        // Act
        var result = await _controller.Register(request2, CancellationToken.None);

        // Assert
        var badRequest = result.Result as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
        badRequest.Should().NotBeNull();
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_ReturnsBadRequest()
    {
        // Arrange
        var request1 = CreateValidRequest(username: "sameuser");
        var request2 = CreateValidRequest(username: "sameuser");

        await _controller.Register(request1, CancellationToken.None);

        // Act
        var result = await _controller.Register(request2, CancellationToken.None);

        // Assert
        var badRequest = result.Result as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
        badRequest.Should().NotBeNull();
    }

    [Fact]
    public async Task Register_WithShortPassword_ReturnsBadRequest()
    {
        // Arrange
        var request = CreateValidRequest();
        var badRequest = request with { Password = "short" };

        // Act
        var result = await _controller.Register(badRequest, CancellationToken.None);

        // Assert
        var badRequestResult = result.Result as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public async Task Register_WithEmptyCompanyName_ReturnsBadRequest()
    {
        // Arrange
        var request = CreateValidRequest();
        var badRequest = request with { CompanyName = "" };

        // Act
        var result = await _controller.Register(badRequest, CancellationToken.None);

        // Assert
        var badRequestResult = result.Result as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public async Task GetStatus_WithValidRequestId_ReturnsStatus()
    {
        // Arrange
        var request = CreateValidRequest();
        var registerResult = await _controller.Register(request, CancellationToken.None);
        var okResult = registerResult.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        var response = okResult!.Value as BusinessRegistrationResponse;

        // Act
        var statusResult = await _controller.GetStatus(response!.RequestId, CancellationToken.None);

        // Assert
        var statusOk = statusResult.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        statusOk.Should().NotBeNull();
        var statusResponse = statusOk!.Value as RegistrationStatusResponse;
        statusResponse.Should().NotBeNull();
        statusResponse!.Status.Should().Be("Submitted");
    }

    [Fact]
    public async Task GetStatus_WithInvalidRequestId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetStatus(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.NotFoundObjectResult>();
    }

    [Fact]
    public async Task Register_PendingActivationUser_CannotLogin()
    {
        // Arrange
        var request = CreateValidRequest();
        await _controller.Register(request, CancellationToken.None);

        var authController = new AuthController(new AuthService(
            new UserAccountRepository(_context),
            new FakeMemberAccountRepository(),
            new FakeJwtTokenService()));

        // Act
        var loginResult = await authController.Login(
            new LoginRequest(request.Username, request.Password), CancellationToken.None);

        // Assert
        var unauthorized = loginResult.Result as Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult;
        unauthorized.Should().NotBeNull();
        unauthorized!.Value.Should().BeEquivalentTo(new { error = "Account is pending activation." });
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
        public Task<MemberAccount> AddAsync(MemberAccount entity, CancellationToken cancellationToken = default) => Task.FromResult(entity);
        public void Update(MemberAccount entity) { }
        public void Delete(MemberAccount entity) { }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
