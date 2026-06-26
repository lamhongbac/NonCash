using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NonCash.API.Controllers;
using NonCash.API.DTOs;
using NonCash.API.Services;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;
using NonCash.Infrastructure.Data;
using NonCash.Infrastructure.Repositories;

namespace NonCash.IntegrationTests.Controllers;

public class AuthControllerTests
{
    private readonly ApplicationDbContext _context;
    private readonly IAuthService _authService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUserAccountRepository _userRepository;
    private readonly IMemberAccountRepository _memberRepository;

    public AuthControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _userRepository = new UserAccountRepository(_context);
        _memberRepository = new MemberAccountRepository(_context);

        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Jwt:Key", "noncash-test-key-min-32-bytes-long!!" },
            { "Jwt:Issuer", "NonCash-Test" },
            { "Jwt:Audience", "NonCash-Test-Users" },
            { "Jwt:ExpiryHours", "1" }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        _jwtTokenService = new JwtTokenService(configuration);
        _authService = new AuthService(_userRepository, _memberRepository, _jwtTokenService);
    }

    private AuthController CreateController()
    {
        return new AuthController(_authService);
    }

    private UsersController CreateUsersController()
    {
        var userService = new UserService(_userRepository, _authService);
        return new UsersController(userService);
    }

    private async Task<UserAccount> SeedUser(string username = "testuser", UserRole role = UserRole.BrandManager, UserStatus status = UserStatus.Active)
    {
        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = _authService.HashPassword("Password@123"),
            FullName = "Test User",
            Role = role,
            BrandId = role == UserRole.Admin ? null : Guid.NewGuid(),
            Status = status
        };

        await _userRepository.AddAsync(user);
        await _userRepository.SaveChangesAsync();
        return user;
    }

    private async Task<MemberAccount> SeedMember(string username = "testmember", MemberAccountStatus status = MemberAccountStatus.Active)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "0909111111",
            FullName = "Test Member",
            Status = CustomerStatus.Active
        };
        await _context.Customers.AddAsync(customer);
        await _context.SaveChangesAsync();

        var member = new MemberAccount
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Username = username,
            PasswordHash = _authService.HashPassword("Password@123"),
            FullName = "Test Member",
            Status = status
        };

        await _memberRepository.AddAsync(member);
        await _memberRepository.SaveChangesAsync();
        return member;
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        // Arrange
        var user = await SeedUser();
        var controller = CreateController();

        // Act
        var result = await controller.Login(new LoginRequest("testuser", "Password@123"), CancellationToken.None);

        // Assert
        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        okResult.Should().NotBeNull();
        var response = okResult!.Value as LoginResponse;
        response.Should().NotBeNull();
        response!.Token.Should().NotBeNullOrEmpty();
        response.User.UserId.Should().Be(user.Id);
        response.User.Role.Should().Be("BrandManager");
    }

    [Fact]
    public async Task MemberLogin_WithValidCredentials_ReturnsToken()
    {
        // Arrange
        var member = await SeedMember();
        var controller = CreateController();

        // Act
        var result = await controller.MemberLogin(new LoginRequest("testmember", "Password@123"), CancellationToken.None);

        // Assert
        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        okResult.Should().NotBeNull();
        var response = okResult!.Value as LoginResponse;
        response.Should().NotBeNull();
        response!.Token.Should().NotBeNullOrEmpty();
        response.User.UserId.Should().Be(member.Id);
        response.User.Role.Should().Be("Member");
        response.User.CustomerId.Should().Be(member.CustomerId);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        await SeedUser();
        var controller = CreateController();

        // Act
        var result = await controller.Login(new LoginRequest("testuser", "WrongPassword"), CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_WithNonexistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.Login(new LoginRequest("nonexistent", "Password@123"), CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_WithLockedAccount_ReturnsForbid()
    {
        // Arrange
        await SeedUser(status: UserStatus.Locked);
        var controller = CreateController();

        // Act
        var result = await controller.Login(new LoginRequest("testuser", "Password@123"), CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.ForbidResult>();
    }

    [Fact]
    public async Task Login_WithEmptyCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.Login(new LoginRequest("", ""), CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task CreateUser_WithValidData_ReturnsCreated()
    {
        // Arrange
        var controller = CreateUsersController();
        var brandId = Guid.NewGuid();
        var request = new CreateUserRequest("newuser", "Password@123", "New User", "BrandManager", brandId);

        // Act
        var result = await controller.CreateUser(request, CancellationToken.None);

        // Assert
        var createdResult = result.Result as Microsoft.AspNetCore.Mvc.CreatedAtActionResult;
        createdResult.Should().NotBeNull();
        var response = createdResult!.Value as UserResponse;
        response.Should().NotBeNull();
        response!.Username.Should().Be("newuser");
        response.Role.Should().Be("BrandManager");
        response.BrandId.Should().Be(brandId);
    }

    [Fact]
    public async Task CreateUser_WithDuplicateUsername_ReturnsConflict()
    {
        // Arrange
        await SeedUser();
        var controller = CreateUsersController();
        var request = new CreateUserRequest("testuser", "Password@456", "Another User", "Planner", Guid.NewGuid());

        // Act
        var result = await controller.CreateUser(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.ConflictObjectResult>();
    }

    [Fact]
    public async Task CreateUser_WithShortPassword_ReturnsBadRequest()
    {
        // Arrange
        var controller = CreateUsersController();
        var request = new CreateUserRequest("newuser", "short", "New User", "BrandManager", Guid.NewGuid());

        // Act
        var result = await controller.CreateUser(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateUser_WithInvalidRole_ReturnsBadRequest()
    {
        // Arrange
        var controller = CreateUsersController();
        var request = new CreateUserRequest("newuser", "Password@123", "New User", "InvalidRole", Guid.NewGuid());

        // Act
        var result = await controller.CreateUser(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>();
    }

    [Fact]
    public async Task LockUser_WithExistingUser_ReturnsLockedStatus()
    {
        // Arrange
        var user = await SeedUser();
        var controller = CreateUsersController();

        // Act
        var result = await controller.LockUser(user.Id, CancellationToken.None);

        // Assert
        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        okResult.Should().NotBeNull();
        var response = okResult!.Value as UserResponse;
        response.Should().NotBeNull();
        response!.Status.Should().Be("Locked");
    }

    [Fact]
    public async Task UnlockUser_WithLockedUser_ReturnsActiveStatus()
    {
        // Arrange
        var user = await SeedUser(status: UserStatus.Locked);
        var controller = CreateUsersController();

        // Act
        var result = await controller.UnlockUser(user.Id, CancellationToken.None);

        // Assert
        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        okResult.Should().NotBeNull();
        var response = okResult!.Value as UserResponse;
        response.Should().NotBeNull();
        response!.Status.Should().Be("Active");
    }

    [Fact]
    public async Task GetUsers_ReturnsAllUsers()
    {
        // Arrange
        await SeedUser("user1");
        await SeedUser("user2");
        var controller = CreateUsersController();

        // Act
        var result = await controller.GetUsers(null, CancellationToken.None);

        // Assert
        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        okResult.Should().NotBeNull();
        var users = okResult!.Value as IEnumerable<UserResponse>;
        users.Should().NotBeNull();
        users!.Count().Should().Be(2);
    }
}

public class JwtTokenServiceTests
{
    private JwtTokenService CreateService()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Jwt:Key", "noncash-test-key-min-32-bytes-long!!" },
            { "Jwt:Issuer", "NonCash-Test" },
            { "Jwt:Audience", "NonCash-Test-Users" },
            { "Jwt:ExpiryHours", "1" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        return new JwtTokenService(configuration);
    }

    [Fact]
    public void GenerateToken_WithValidUser_ReturnsNonEmptyToken()
    {
        var service = CreateService();
        var user = new UserAccount
        {
            Id = Guid.NewGuid(), Username = "testuser", FullName = "Test User",
            Role = UserRole.BrandManager, BrandId = Guid.NewGuid(), Status = UserStatus.Active
        };

        var token = service.GenerateToken(user);
        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateToken_WithValidMember_ReturnsNonEmptyToken()
    {
        var service = CreateService();
        var member = new MemberAccount
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            Username = "testmember",
            FullName = "Test Member",
            Status = MemberAccountStatus.Active
        };

        var token = service.GenerateToken(member);
        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateToken_ContainsCorrectClaims()
    {
        var service = CreateService();
        var user = new UserAccount
        {
            Id = Guid.NewGuid(), Username = "testuser", FullName = "Test User",
            Role = UserRole.BrandManager, BrandId = Guid.NewGuid(), Status = UserStatus.Active
        };

        var token = service.GenerateToken(user);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var subClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub");
        subClaim.Should().NotBeNull();
        subClaim!.Value.Should().Be(user.Id.ToString());

        var roleClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
        roleClaim.Should().NotBeNull();
        roleClaim!.Value.Should().Be("BrandManager");

        var brandIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "brand_id");
        brandIdClaim.Should().NotBeNull();
        brandIdClaim!.Value.Should().Be(user.BrandId.ToString());
    }

    [Fact]
    public void GenerateToken_MemberContainsCorrectClaims()
    {
        var service = CreateService();
        var member = new MemberAccount
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            Username = "testmember",
            FullName = "Test Member",
            Status = MemberAccountStatus.Active
        };

        var token = service.GenerateToken(member);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var subClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub");
        subClaim.Should().NotBeNull();
        subClaim!.Value.Should().Be(member.Id.ToString());

        var roleClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
        roleClaim.Should().NotBeNull();
        roleClaim!.Value.Should().Be("Member");

        var customerIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "customer_id");
        customerIdClaim.Should().NotBeNull();
        customerIdClaim!.Value.Should().Be(member.CustomerId.ToString());
    }

    [Fact]
    public void GenerateToken_AdminUser_HasAdminRole()
    {
        var service = CreateService();
        var user = new UserAccount
        {
            Id = Guid.NewGuid(), Username = "admin", FullName = "Admin",
            Role = UserRole.Admin, BrandId = null
        };

        var token = service.GenerateToken(user);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var roleClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
        roleClaim.Should().NotBeNull();
        roleClaim!.Value.Should().Be("Admin");
    }

    [Fact]
    public void GetTokenExpiry_ReturnsFutureDate()
    {
        var service = CreateService();
        var before = DateTime.UtcNow.AddMinutes(55);
        var expiry = service.GetTokenExpiry();
        var after = DateTime.UtcNow.AddHours(1).AddMinutes(5);
        expiry.Should().BeAfter(before);
        expiry.Should().BeBefore(after);
    }

    [Fact]
    public void GenerateToken_IsProperlySigned()
    {
        var service = CreateService();
        var user = new UserAccount
        {
            Id = Guid.NewGuid(), Username = "testuser", FullName = "Test User",
            Role = UserRole.BrandManager, BrandId = Guid.NewGuid(), Status = UserStatus.Active
        };
        var token = service.GenerateToken(user);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = false,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "NonCash-Test", ValidAudience = "NonCash-Test-Users",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("noncash-test-key-min-32-bytes-long!!"))
        };

        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(token, validationParameters, out var validatedToken);
        principal.Should().NotBeNull();
        validatedToken.Should().NotBeNull();
    }
}
