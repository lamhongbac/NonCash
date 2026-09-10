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
using NonCash.Infrastructure.Services;

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
            { "Jwt:ExpiryHours", "1" },
            { "WebBaseUrl", "https://test.noncash.local" }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        _jwtTokenService = new JwtTokenService(configuration);
        _authService = new AuthService(_userRepository, _memberRepository, _jwtTokenService, new FileNotificationService(), new CustomerRepository(_context), new OutletRepository(_context), new UserOutletRepository(_context), configuration);
    }

    private AuthController CreateController()
    {
        return new AuthController(_authService);
    }

    private UsersController CreateUsersController()
    {
        var userService = new UserService(_userRepository, _authService, new FileNotificationService(), new FakeBrandRepositoryForAuth());
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

    private async Task<MemberAccount> SeedMember(MemberAccountStatus status = MemberAccountStatus.Active)
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
            PasswordHash = _authService.HashPassword("Password@123"),
            FullName = "Test Member",
            Status = status
        };

        await _memberRepository.AddAsync(member);
        await _memberRepository.SaveChangesAsync();
        return member;
    }

    /// <summary>CR-27: the login identifier lives on the customer record, so seeding a member
    /// means seeding the phone number and/or email the API will resolve it from.</summary>
    private async Task<MemberAccount> SeedMemberWithIdentifier(
        string phone, string? email = null, string? passwordHash = null)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            PhoneNumber = Customer.NormalizePhoneNumber(phone),
            Email = Customer.NormalizeEmail(email),
            FullName = "Phone Member",
            Status = CustomerStatus.Active
        };
        await _context.Customers.AddAsync(customer);
        await _context.SaveChangesAsync();

        var member = new MemberAccount
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            PasswordHash = passwordHash ?? _authService.HashPassword("Password@123"),
            FullName = "Phone Member",
            Status = MemberAccountStatus.Active
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

    private static string? Error(Microsoft.AspNetCore.Mvc.ObjectResult result) =>
        result.Value!.GetType().GetProperty("error")!.GetValue(result.Value) as string;

    [Fact]
    public async Task MemberLogin_WithPhoneNumber_ReturnsToken()
    {
        // Arrange
        var member = await SeedMember();
        var controller = CreateController();

        // Act
        var result = await controller.MemberLogin(new MemberLoginRequest("0909111111", "Password@123"), CancellationToken.None);

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
    public async Task MemberLogin_WithFormattedPhoneNumber_ResolvesTheMemberAccount()
    {
        var member = await SeedMemberWithIdentifier("0913660575", email: "member@example.com");
        var controller = CreateController();

        var result = await controller.MemberLogin(new MemberLoginRequest(" 0913 660 575 ", "Password@123"), CancellationToken.None);

        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        okResult.Should().NotBeNull();
        ((LoginResponse)okResult!.Value!).User.UserId.Should().Be(member.Id);
    }

    [Fact]
    public async Task MemberLogin_WithEmailAddress_ResolvesTheMemberAccount()
    {
        var member = await SeedMemberWithIdentifier("0913660575", email: "Member@Example.com");
        var controller = CreateController();

        var result = await controller.MemberLogin(new MemberLoginRequest("member@example.com", "Password@123"), CancellationToken.None);

        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        okResult.Should().NotBeNull();
        ((LoginResponse)okResult!.Value!).User.UserId.Should().Be(member.Id);
    }

    [Fact]
    public async Task MemberLogin_WithAFormerUsername_TellsTheCustomerWhatToEnter()
    {
        await SeedMemberWithIdentifier("0913660575", email: "member@example.com");
        var controller = CreateController();

        var result = await controller.MemberLogin(new MemberLoginRequest("lamhongbac", "Password@123"), CancellationToken.None);

        var unauthorized = result.Result as Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult;
        unauthorized.Should().NotBeNull();
        Error(unauthorized!).Should().Be(AuthService.MalformedIdentifierMessage);
    }

    [Fact]
    public async Task MemberLogin_ProvisionedAccountWithoutPassword_ReturnsHowToSetOne()
    {
        await SeedMemberWithIdentifier("0913660575", passwordHash: string.Empty);
        var controller = CreateController();

        var result = await controller.MemberLogin(new MemberLoginRequest("0913660575", "Password@123"), CancellationToken.None);

        var unauthorized = result.Result as Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult;
        unauthorized.Should().NotBeNull();
        Error(unauthorized!).Should().Be(AuthService.NoPasswordYetMessage);
    }

    [Fact]
    public async Task MemberLogin_WrongPassword_ReturnsGenericError()
    {
        await SeedMemberWithIdentifier("0913660575");
        var controller = CreateController();

        var result = await controller.MemberLogin(new MemberLoginRequest("0913660575", "WrongPassword"), CancellationToken.None);

        var unauthorized = result.Result as Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult;
        unauthorized.Should().NotBeNull();
        Error(unauthorized!).Should().Be(AuthService.InvalidIdentifierMessage);
    }

    [Fact]
    public async Task MemberLogin_BlacklistedCustomer_IsRefused()
    {
        // Matrix row 10 (O2) survives the switch to phone/email identifiers.
        var member = await SeedMemberWithIdentifier("0913660575");
        var customer = await _context.Customers.SingleAsync(c => c.Id == member.CustomerId);
        customer.Status = CustomerStatus.Blacklisted;
        await _context.SaveChangesAsync();
        var controller = CreateController();

        var result = await controller.MemberLogin(new MemberLoginRequest("0913660575", "Password@123"), CancellationToken.None);

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.ForbidResult>();
    }

    // ── CR-27 C1: POST /auth/member/forgot-password ──────────────────────

    [Fact]
    public async Task MemberForgotPassword_KnownIdentifier_ReturnsTheNeutralMessage()
    {
        await SeedMemberWithIdentifier("0913660575", email: "member@example.com");
        var controller = CreateController();

        var result = await controller.MemberForgotPassword(new MemberForgotPasswordRequest("0913660575"), CancellationToken.None);

        var ok = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        ok.Should().NotBeNull();
        ((MemberForgotPasswordResponse)ok!.Value!).Message.Should().Be(AuthService.SignInLinkSentMessage);
    }

    [Fact]
    public async Task MemberForgotPassword_UnknownIdentifier_ReturnsTheSameMessage()
    {
        // Anti-enumeration: a made-up phone number must be indistinguishable from a real one.
        var controller = CreateController();

        var result = await controller.MemberForgotPassword(new MemberForgotPasswordRequest("0900000000"), CancellationToken.None);

        var ok = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        ok.Should().NotBeNull();
        ((MemberForgotPasswordResponse)ok!.Value!).Message.Should().Be(AuthService.SignInLinkSentMessage);
    }

    [Fact]
    public async Task MemberForgotPassword_MalformedIdentifier_ReturnsBadRequestWithNextAction()
    {
        var controller = CreateController();

        var result = await controller.MemberForgotPassword(new MemberForgotPasswordRequest("not-an-identifier"), CancellationToken.None);

        var bad = result.Result as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
        bad.Should().NotBeNull();
        Error(bad!).Should().Be(AuthService.MalformedIdentifierMessage);
    }

    // ── CR-2026-09-10-32: POST /auth/set-password ────────────────────────

    [Fact]
    public async Task SetMemberPassword_WithAMemberToken_PersistsTheHash()
    {
        var member = await SeedMemberWithIdentifier("0913660575", passwordHash: string.Empty);
        var controller = CreateController();
        controller.ControllerContext = MemberContext(member.Id);

        var result = await controller.SetMemberPassword(new SetMemberPasswordRequest("NewPassword@123"), CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();
        var stored = await _context.MemberAccounts.SingleAsync(m => m.Id == member.Id);
        stored.PasswordHash.Should().NotBeEmpty();
        _authService.VerifyPassword("NewPassword@123", stored.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public void SetMemberPassword_RequiresTheMemberRole()
    {
        // These tests construct the controller directly, so the authorization middleware that
        // turns a missing token into a 401 never runs; assert the attribute that drives it.
        var attribute = typeof(AuthController)
            .GetMethod(nameof(AuthController.SetMemberPassword))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Single();

        attribute.Roles.Should().Be("Member");
    }

    private static Microsoft.AspNetCore.Mvc.ControllerContext MemberContext(Guid memberAccountId)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, memberAccountId.ToString()),
                new Claim(ClaimTypes.Role, "Member")
            },
            authenticationType: "Test");

        return new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
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

    private class FakeBrandRepositoryForAuth : IBrandRepository
    {
        public Task<Brand?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Brand?>(null);
        public Task<Brand> AddAsync(Brand entity, CancellationToken ct = default) => throw new NotImplementedException();
        public void Update(Brand entity) => throw new NotImplementedException();
        public void Delete(Brand entity) => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<Brand>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IEnumerable<Brand>>(Array.Empty<Brand>());
        public Task<IEnumerable<Brand>> FindAsync(System.Linq.Expressions.Expression<Func<Brand, bool>> predicate, CancellationToken ct = default) => Task.FromResult<IEnumerable<Brand>>(Array.Empty<Brand>());
        public Task<int> CountAsync(System.Linq.Expressions.Expression<Func<Brand, bool>> predicate, CancellationToken ct = default) => Task.FromResult(0);
        public Task<bool> TaxCodeExistsAsync(string taxCode, CancellationToken ct = default) => Task.FromResult(false);
        public Task<Brand?> GetByTaxCodeAsync(string taxCode, CancellationToken ct = default) => Task.FromResult<Brand?>(null);
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
    public void GenerateToken_Member_OmitsTheUsernameClaim()
    {
        // CR-27: customers have no username, so the token must not invent one for them.
        var service = CreateService();
        var member = new MemberAccount
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            FullName = "Test Member",
            Status = MemberAccountStatus.Active
        };

        var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(service.GenerateToken(member));

        jwtToken.Claims.Should().NotContain(c => c.Type == ClaimTypes.Name || c.Type == "unique_name");
    }

    [Fact]
    public void GenerateSignInLinkToken_RoundTripsThroughTheMagicLinkValidator()
    {
        // The recovery link lands on the same /member/welcome route as the voucher magic link,
        // so one validator accepts both purposes.
        var service = CreateService();
        var memberId = Guid.NewGuid();

        service.ValidateMagicLinkToken(service.GenerateSignInLinkToken(memberId)).Should().Be(memberId);
        service.ValidateMagicLinkToken(service.GenerateMagicLinkToken(memberId)).Should().Be(memberId);
    }

    [Fact]
    public void GenerateSignInLinkToken_ExpiresInThirtyMinutes()
    {
        // Short-lived on purpose: it is a way into the account, mailed to a possibly shared inbox.
        var service = CreateService();

        var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(service.GenerateSignInLinkToken(Guid.NewGuid()));

        jwtToken.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(30), TimeSpan.FromMinutes(2));
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
