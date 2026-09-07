using FluentAssertions;
using NSubstitute;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;

namespace NonCash.UnitTests.Services;

/// <summary>
/// Member login vs. the customer-action-matrix: the platform blacklist (O2) blocks
/// login, and an un-blacklist restores it instantly (P4 reversibility).
/// </summary>
public class AuthServiceTests
{
    private readonly IUserAccountRepository _userRepository = Substitute.For<IUserAccountRepository>();
    private readonly IMemberAccountRepository _memberRepository = Substitute.For<IMemberAccountRepository>();
    private readonly IJwtTokenService _jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly ICustomerRepository _customerRepository = Substitute.For<ICustomerRepository>();
    private readonly IOutletRepository _outletRepository = Substitute.For<IOutletRepository>();
    private readonly IUserOutletRepository _userOutletRepository = Substitute.For<IUserOutletRepository>();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(
            _userRepository,
            _memberRepository,
            _jwtTokenService,
            Substitute.For<INotificationService>(),
            _customerRepository,
            _outletRepository,
            _userOutletRepository);
    }

    private MemberAccount SeedMember(Guid customerId) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = customerId,
        Username = "member1",
        PasswordHash = _sut.HashPassword("Password@123"),
        FullName = "Test Member",
        Status = MemberAccountStatus.Active
    };

    [Fact]
    public async Task LoginMember_WithActiveCustomer_ReturnsToken()
    {
        // Arrange
        var customer = new Customer { Id = Guid.NewGuid(), Status = CustomerStatus.Active };
        var member = SeedMember(customer.Id);
        _memberRepository.GetByUsernameAsync("member1", Arg.Any<CancellationToken>()).Returns(member);
        _customerRepository.GetByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(customer);
        _jwtTokenService.GenerateToken(Arg.Any<MemberAccount>()).Returns("token");
        _jwtTokenService.GetTokenExpiry().Returns(DateTime.UtcNow.AddHours(1));

        // Act
        var result = await _sut.LoginMemberAsync("member1", "Password@123");

        // Assert
        result.Success.Should().BeTrue();
        result.Token.Should().Be("token");
        result.Member.Should().Be(member);
    }

    [Fact]
    public async Task LoginMember_BlacklistedCustomer_Rejected()
    {
        // Matrix row 10 (O2): platform-blacklisted customers cannot log in. The
        // message matches the locked-account wording — no blacklist detail leak.
        // Arrange
        var customer = new Customer { Id = Guid.NewGuid(), Status = CustomerStatus.Blacklisted };
        var member = SeedMember(customer.Id);
        _memberRepository.GetByUsernameAsync("member1", Arg.Any<CancellationToken>()).Returns(member);
        _customerRepository.GetByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(customer);

        // Act
        var result = await _sut.LoginMemberAsync("member1", "Password@123");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Account is locked.");
        _jwtTokenService.DidNotReceive().GenerateToken(Arg.Any<MemberAccount>());
    }

    [Fact]
    public async Task LoginMember_AfterUnblacklist_CanLogInAgain()
    {
        // P4 reversibility: the check reads Customer.Status directly and never mirrors
        // it into MemberAccount.Status, so un-blacklisting restores login instantly.
        // Arrange
        var customer = new Customer { Id = Guid.NewGuid(), Status = CustomerStatus.Blacklisted };
        var member = SeedMember(customer.Id);
        _memberRepository.GetByUsernameAsync("member1", Arg.Any<CancellationToken>()).Returns(member);
        _customerRepository.GetByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(customer);
        _jwtTokenService.GenerateToken(Arg.Any<MemberAccount>()).Returns("token");
        _jwtTokenService.GetTokenExpiry().Returns(DateTime.UtcNow.AddHours(1));

        (await _sut.LoginMemberAsync("member1", "Password@123")).Success.Should().BeFalse();

        // Act — admin un-blacklists the customer (status restored, member untouched)
        customer.Status = CustomerStatus.Active;
        var result = await _sut.LoginMemberAsync("member1", "Password@123");

        // Assert
        result.Success.Should().BeTrue();
        member.Status.Should().Be(MemberAccountStatus.Active);
    }

    // ── CR-18 LoginStaffAsync ───────────────────────────────────────────

    private UserAccount SeedStaffUser()
    {
        var brandId = Guid.NewGuid();
        return new UserAccount
        {
            Id = Guid.NewGuid(),
            Username = "cashier1",
            PasswordHash = _sut.HashPassword("Staff@1234"),
            FullName = "Store Cashier",
            Role = UserRole.StoreStaff,
            BrandId = brandId,
            Status = UserStatus.Active
        };
    }

    [Fact]
    public async Task LoginStaff_ValidCredentials_ReturnsTokenWithOutletId()
    {
        var staff = SeedStaffUser();
        var outlet = new Outlet { Id = Guid.NewGuid(), BrandId = staff.BrandId!.Value, Code = "S01", Name = "Store 1", Status = OutletStatus.Active };
        _userRepository.GetByUsernameAsync("cashier1", Arg.Any<CancellationToken>()).Returns(staff);
        _outletRepository.GetByCodeAsync(staff.BrandId.Value, "S01", Arg.Any<CancellationToken>()).Returns(outlet);
        _userOutletRepository.GetOutletsForUserAsync(staff.Id, Arg.Any<CancellationToken>()).Returns(new[] { outlet });
        _jwtTokenService.GenerateToken(Arg.Any<UserAccount>(), outlet.Id).Returns("staff-token");
        _jwtTokenService.GetTokenExpiry().Returns(DateTime.UtcNow.AddHours(8));

        var result = await _sut.LoginStaffAsync("cashier1", "S01", "Staff@1234");

        result.Success.Should().BeTrue();
        result.Token.Should().Be("staff-token");
        result.OutletId.Should().Be(outlet.Id);
    }

    [Fact]
    public async Task LoginStaff_UnknownUser_GenericError()
    {
        _userRepository.GetByUsernameAsync("nobody", Arg.Any<CancellationToken>()).Returns((UserAccount?)null);

        var result = await _sut.LoginStaffAsync("nobody", "S01", "Staff@1234");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid credentials.");
    }

    [Fact]
    public async Task LoginStaff_WrongPassword_GenericError()
    {
        var staff = SeedStaffUser();
        _userRepository.GetByUsernameAsync("cashier1", Arg.Any<CancellationToken>()).Returns(staff);

        var result = await _sut.LoginStaffAsync("cashier1", "S01", "WrongPass");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid credentials.");
        _outletRepository.DidNotReceive().GetByCodeAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginStaff_UnknownStoreCode_GenericError()
    {
        var staff = SeedStaffUser();
        _userRepository.GetByUsernameAsync("cashier1", Arg.Any<CancellationToken>()).Returns(staff);
        _outletRepository.GetByCodeAsync(staff.BrandId!.Value, "NOPE", Arg.Any<CancellationToken>()).Returns((Outlet?)null);

        var result = await _sut.LoginStaffAsync("cashier1", "NOPE", "Staff@1234");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid credentials.");
    }

    [Fact]
    public async Task LoginStaff_NotAssignedToOutlet_GenericError()
    {
        var staff = SeedStaffUser();
        var outlet = new Outlet { Id = Guid.NewGuid(), BrandId = staff.BrandId!.Value, Code = "S02", Name = "Other", Status = OutletStatus.Active };
        _userRepository.GetByUsernameAsync("cashier1", Arg.Any<CancellationToken>()).Returns(staff);
        _outletRepository.GetByCodeAsync(staff.BrandId.Value, "S02", Arg.Any<CancellationToken>()).Returns(outlet);
        _userOutletRepository.GetOutletsForUserAsync(staff.Id, Arg.Any<CancellationToken>()).Returns(Enumerable.Empty<Outlet>());

        var result = await _sut.LoginStaffAsync("cashier1", "S02", "Staff@1234");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid credentials.");
    }

    [Fact]
    public async Task LoginStaff_LockedAccount_GenericError()
    {
        var staff = SeedStaffUser();
        staff.Status = UserStatus.Locked;
        _userRepository.GetByUsernameAsync("cashier1", Arg.Any<CancellationToken>()).Returns(staff);

        var result = await _sut.LoginStaffAsync("cashier1", "S01", "Staff@1234");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid credentials.");
    }

    [Fact]
    public async Task LoginStaff_EmptyInput_GenericError()
    {
        var result = await _sut.LoginStaffAsync("", "S01", "Staff@1234");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid credentials.");
    }
}
