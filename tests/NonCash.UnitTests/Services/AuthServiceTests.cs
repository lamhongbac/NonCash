using FluentAssertions;
using Microsoft.Extensions.Configuration;
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
    private readonly INotificationService _notificationService = Substitute.For<INotificationService>();
    private readonly IConfiguration _configuration = Substitute.For<IConfiguration>();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _configuration["WebBaseUrl"].Returns("https://test.noncash.local");
        _sut = new AuthService(
            _userRepository,
            _memberRepository,
            _jwtTokenService,
            _notificationService,
            _customerRepository,
            _outletRepository,
            _userOutletRepository,
            _configuration);
    }

    private const string Phone = "0913660575";
    private const string Email = "member@example.com";
    private const string Password = "Password@123";

    /// <summary>Seeds a customer + member pair and stubs every lookup the identifier
    /// resolution can take: phone, email, and the blacklist read-back by customer id.</summary>
    private (Customer Customer, MemberAccount Member) SeedMember(
        string phone = Phone, string? email = null, string? password = Password)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            PhoneNumber = phone,
            Email = email,
            FullName = "Test Member",
            Status = CustomerStatus.Active
        };
        var member = new MemberAccount
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            PasswordHash = password is null ? string.Empty : _sut.HashPassword(password),
            FullName = customer.FullName,
            Status = MemberAccountStatus.Active
        };

        _customerRepository.GetByPhoneNumberAsync(phone, Arg.Any<CancellationToken>()).Returns(customer);
        if (email is not null)
            _customerRepository.GetByEmailAsync(email, Arg.Any<CancellationToken>()).Returns(customer);
        _memberRepository.GetByCustomerIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(member);
        _customerRepository.GetByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(customer);

        return (customer, member);
    }

    [Fact]
    public async Task LoginMember_WithActiveCustomer_ReturnsToken()
    {
        // Arrange
        var (_, member) = SeedMember();
        _jwtTokenService.GenerateToken(Arg.Any<MemberAccount>()).Returns("token");
        _jwtTokenService.GetTokenExpiry().Returns(DateTime.UtcNow.AddHours(1));

        // Act
        var result = await _sut.LoginMemberAsync(Phone, Password);

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
        var (customer, _) = SeedMember();
        customer.Status = CustomerStatus.Blacklisted;

        // Act
        var result = await _sut.LoginMemberAsync(Phone, Password);

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
        var (customer, member) = SeedMember();
        customer.Status = CustomerStatus.Blacklisted;
        _jwtTokenService.GenerateToken(Arg.Any<MemberAccount>()).Returns("token");
        _jwtTokenService.GetTokenExpiry().Returns(DateTime.UtcNow.AddHours(1));

        (await _sut.LoginMemberAsync(Phone, Password)).Success.Should().BeFalse();

        // Act — admin un-blacklists the customer (status restored, member untouched)
        customer.Status = CustomerStatus.Active;
        var result = await _sut.LoginMemberAsync(Phone, Password);

        // Assert
        result.Success.Should().BeTrue();
        member.Status.Should().Be(MemberAccountStatus.Active);
    }

    // ── CR-26 / CR-27 customer sign-in by phone number or email ──────────

    [Fact]
    public async Task LoginMember_WithPhoneNumber_ResolvesTheMemberAccount()
    {
        var (_, member) = SeedMember(email: Email);

        var result = await _sut.LoginMemberAsync(Phone, Password);

        result.Success.Should().BeTrue();
        result.Member.Should().Be(member);
        await _customerRepository.DidNotReceiveWithAnyArgs().GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginMember_WithFormattedPhoneNumber_NormalizesBeforeLookup()
    {
        SeedMember();

        var result = await _sut.LoginMemberAsync(" 0913 660 575 ", Password);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task LoginMember_WithEmailAddress_ResolvesTheMemberAccount()
    {
        var (_, member) = SeedMember(email: Email);

        var result = await _sut.LoginMemberAsync(Email, Password);

        result.Success.Should().BeTrue();
        result.Member.Should().Be(member);
        await _customerRepository.DidNotReceiveWithAnyArgs().GetByPhoneNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginMember_WithMixedCaseEmailAddress_NormalizesBeforeLookup()
    {
        // Emails are stored lowercase, so " Member@Example.com " must still land on the account.
        SeedMember(email: Email);

        var result = await _sut.LoginMemberAsync(" Member@Example.com ", Password);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task LoginMember_ProvisionedAccountWithoutPassword_ExplainsHowToSetOne()
    {
        // Distribution/gifting create the member with an empty hash; "invalid password" here
        // would send the customer in a circle because no password exists to type.
        SeedMember(password: null);

        var result = await _sut.LoginMemberAsync(Phone, "anything123");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be(AuthService.NoPasswordYetMessage);
        result.ErrorMessage.Should().Contain("no password yet").And.Contain("Forgot password");
    }

    [Fact]
    public async Task LoginMember_WrongPassword_GenericError()
    {
        SeedMember(email: Email);

        var result = await _sut.LoginMemberAsync(Phone, "WrongPass123");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be(AuthService.InvalidIdentifierMessage);
    }

    [Fact]
    public async Task LoginMember_UnknownPhoneNumber_GenericError()
    {
        _customerRepository.GetByPhoneNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Customer?)null);

        var result = await _sut.LoginMemberAsync("0900000000", Password);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be(AuthService.InvalidIdentifierMessage);
        _jwtTokenService.DidNotReceive().GenerateToken(Arg.Any<MemberAccount>());
    }

    [Fact]
    public async Task LoginMember_UnknownEmailAddress_GenericError()
    {
        _customerRepository.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Customer?)null);

        var result = await _sut.LoginMemberAsync("nobody@example.com", Password);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be(AuthService.InvalidIdentifierMessage);
    }

    [Fact]
    public async Task LoginMember_IdentifierThatIsNeitherPhoneNorEmail_SaysWhatToEnter()
    {
        // A leftover username is no longer an identifier. Guessing "invalid password" would send
        // the customer retrying a password that was never the problem.
        var result = await _sut.LoginMemberAsync("lamhongbac", Password);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be(AuthService.MalformedIdentifierMessage);
        await _customerRepository.DidNotReceiveWithAnyArgs().GetByPhoneNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _customerRepository.DidNotReceiveWithAnyArgs().GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginMember_MissingIdentifierOrPassword_SaysWhatToEnter()
    {
        var result = await _sut.LoginMemberAsync("  ", Password);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Enter your phone number or email, and your password.");
    }

    // ── CR-27 C1: member forgot-password sends a short-lived sign-in link ──

    [Fact]
    public async Task MemberForgotPassword_KnownIdentifierWithEmail_SendsSignInLink()
    {
        var (customer, member) = SeedMember(email: Email);
        _jwtTokenService.GenerateSignInLinkToken(member.Id).Returns("sign-in-token");

        var result = await _sut.MemberForgotPasswordAsync(Phone);

        result.Success.Should().BeTrue();
        result.Message.Should().Be(AuthService.SignInLinkSentMessage);
        await _notificationService.Received(1).NotifySignInLinkAsync(
            Arg.Is<SignInLinkNotification>(n =>
                n.MemberEmail == Email
                && n.SignInLinkUrl == "https://test.noncash.local/member/welcome?token=sign-in-token"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MemberForgotPassword_RequestedWithEmail_SendsToTheSameAddress()
    {
        var (_, member) = SeedMember(email: Email);
        _jwtTokenService.GenerateSignInLinkToken(member.Id).Returns("sign-in-token");

        var result = await _sut.MemberForgotPasswordAsync(Email);

        result.Success.Should().BeTrue();
        await _notificationService.Received(1).NotifySignInLinkAsync(
            Arg.Is<SignInLinkNotification>(n => n.MemberEmail == Email), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MemberForgotPassword_UnknownIdentifier_AnswersTheSameWithoutSending()
    {
        // Anti-enumeration: the reply must not differ from the "we sent it" case.
        _customerRepository.GetByPhoneNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Customer?)null);

        var result = await _sut.MemberForgotPasswordAsync("0900000000");

        result.Success.Should().BeTrue();
        result.Message.Should().Be(AuthService.SignInLinkSentMessage);
        await _notificationService.DidNotReceiveWithAnyArgs().NotifySignInLinkAsync(
            Arg.Any<SignInLinkNotification>(), Arg.Any<CancellationToken>());
        _jwtTokenService.DidNotReceive().GenerateSignInLinkToken(Arg.Any<Guid>());
    }

    [Fact]
    public async Task MemberForgotPassword_CustomerWithoutEmail_AnswersTheSameWithoutSending()
    {
        SeedMember(email: null);

        var result = await _sut.MemberForgotPasswordAsync(Phone);

        result.Success.Should().BeTrue();
        result.Message.Should().Be(AuthService.SignInLinkSentMessage)
            .And.Contain("contact the brand");
        await _notificationService.DidNotReceiveWithAnyArgs().NotifySignInLinkAsync(
            Arg.Any<SignInLinkNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MemberForgotPassword_BlacklistedCustomer_DoesNotSendAWayIn()
    {
        // Matrix row 10 (O2) — a mailed link must not bypass the platform blacklist, and the
        // reply stays neutral so the endpoint still reveals nothing.
        var (customer, _) = SeedMember(email: Email);
        customer.Status = CustomerStatus.Blacklisted;

        var result = await _sut.MemberForgotPasswordAsync(Phone);

        result.Success.Should().BeTrue();
        result.Message.Should().Be(AuthService.SignInLinkSentMessage);
        await _notificationService.DidNotReceiveWithAnyArgs().NotifySignInLinkAsync(
            Arg.Any<SignInLinkNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MemberForgotPassword_MalformedIdentifier_SaysWhatToEnter()
    {
        // Validation, not account disclosure: nothing was looked up, so nothing is revealed.
        var result = await _sut.MemberForgotPasswordAsync("lamhongbac");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be(AuthService.MalformedIdentifierMessage);
        await _customerRepository.DidNotReceiveWithAnyArgs().GetByPhoneNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MemberForgotPassword_EmptyIdentifier_SaysWhatToEnter()
    {
        var result = await _sut.MemberForgotPasswordAsync("   ");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be(AuthService.MalformedIdentifierMessage);
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
        result.OutletRedemptionMode.Should().Be(PosRedemptionMode.OneClick);
    }

    [Fact]
    public async Task LoginStaff_ThreeStepOutlet_ReportsThreeStepMode()
    {
        var staff = SeedStaffUser();
        var outlet = new Outlet
        {
            Id = Guid.NewGuid(),
            BrandId = staff.BrandId!.Value,
            Code = "S01",
            Name = "Store 1",
            Status = OutletStatus.Active,
            PosRedemptionMode = PosRedemptionMode.ThreeStep
        };
        _userRepository.GetByUsernameAsync("cashier1", Arg.Any<CancellationToken>()).Returns(staff);
        _outletRepository.GetByCodeAsync(staff.BrandId.Value, "S01", Arg.Any<CancellationToken>()).Returns(outlet);
        _userOutletRepository.GetOutletsForUserAsync(staff.Id, Arg.Any<CancellationToken>()).Returns(new[] { outlet });
        _jwtTokenService.GenerateToken(Arg.Any<UserAccount>(), outlet.Id).Returns("staff-token");
        _jwtTokenService.GetTokenExpiry().Returns(DateTime.UtcNow.AddHours(8));

        var result = await _sut.LoginStaffAsync("cashier1", "S01", "Staff@1234");

        result.Success.Should().BeTrue();
        result.OutletRedemptionMode.Should().Be(PosRedemptionMode.ThreeStep);
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

    // ── CR-2026-09-07-19 Magic Link Login ────────────────────────────────

    [Fact]
    public async Task MagicLinkLogin_ValidToken_ReturnsSessionToken()
    {
        var memberId = Guid.NewGuid();
        var customer = new Customer { Id = Guid.NewGuid(), Status = CustomerStatus.Active };
        var member = new MemberAccount
        {
            Id = memberId, CustomerId = customer.Id,
            PasswordHash = string.Empty, FullName = "Magic User", Status = MemberAccountStatus.Active
        };
        _jwtTokenService.ValidateMagicLinkToken("valid-token").Returns(memberId);
        _memberRepository.GetByIdAsync(memberId, Arg.Any<CancellationToken>()).Returns(member);
        _customerRepository.GetByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(customer);
        _jwtTokenService.GenerateToken(Arg.Any<MemberAccount>()).Returns("session-jwt");
        _jwtTokenService.GetTokenExpiry().Returns(DateTime.UtcNow.AddHours(8));

        var result = await _sut.MagicLinkLoginAsync("valid-token");

        result.Success.Should().BeTrue();
        result.Token.Should().Be("session-jwt");
        result.Member!.FullName.Should().Be("Magic User");
    }

    [Fact]
    public async Task MagicLinkLogin_InvalidToken_Rejected()
    {
        _jwtTokenService.ValidateMagicLinkToken("bad-token").Returns((Guid?)null);

        var result = await _sut.MagicLinkLoginAsync("bad-token");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid");
    }

    [Fact]
    public async Task MagicLinkLogin_LockedMember_Rejected()
    {
        var memberId = Guid.NewGuid();
        var member = new MemberAccount
        {
            Id = memberId, CustomerId = Guid.NewGuid(),
            PasswordHash = "", FullName = "Locked", Status = MemberAccountStatus.Locked
        };
        _jwtTokenService.ValidateMagicLinkToken("token").Returns(memberId);
        _memberRepository.GetByIdAsync(memberId, Arg.Any<CancellationToken>()).Returns(member);

        var result = await _sut.MagicLinkLoginAsync("token");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("locked");
    }

    [Fact]
    public async Task MagicLinkLogin_BlacklistedCustomer_Rejected()
    {
        var memberId = Guid.NewGuid();
        var customer = new Customer { Id = Guid.NewGuid(), Status = CustomerStatus.Blacklisted };
        var member = new MemberAccount
        {
            Id = memberId, CustomerId = customer.Id,
            PasswordHash = "", FullName = "BL", Status = MemberAccountStatus.Active
        };
        _jwtTokenService.ValidateMagicLinkToken("token").Returns(memberId);
        _memberRepository.GetByIdAsync(memberId, Arg.Any<CancellationToken>()).Returns(member);
        _customerRepository.GetByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(customer);

        var result = await _sut.MagicLinkLoginAsync("token");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("locked");
    }

    // ── CR-2026-09-07-19 Set Member Password ────────────────────────────

    [Fact]
    public async Task SetMemberPassword_ValidInput_Succeeds()
    {
        var member = new MemberAccount
        {
            Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(),
            PasswordHash = string.Empty, FullName = "Test", Status = MemberAccountStatus.Active
        };
        _memberRepository.GetByIdAsync(member.Id, Arg.Any<CancellationToken>()).Returns(member);

        var result = await _sut.SetMemberPasswordAsync(member.Id, "NewPassword123");

        result.Should().BeTrue();
        member.PasswordHash.Should().NotBe(string.Empty);
        _memberRepository.Received(1).Update(member);
    }

    [Fact]
    public async Task SetMemberPassword_TooShort_Fails()
    {
        var result = await _sut.SetMemberPasswordAsync(Guid.NewGuid(), "short");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetMemberPassword_MemberNotFound_Fails()
    {
        _memberRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((MemberAccount?)null);

        var result = await _sut.SetMemberPasswordAsync(Guid.NewGuid(), "LongEnoughPassword");

        result.Should().BeFalse();
    }
}
