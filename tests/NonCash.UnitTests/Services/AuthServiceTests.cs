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
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(
            _userRepository,
            _memberRepository,
            _jwtTokenService,
            Substitute.For<INotificationService>(),
            _customerRepository);
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
}
