using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public class AuthService : IAuthService
{
    private readonly IUserAccountRepository _userRepository;
    private readonly IMemberAccountRepository _memberRepository;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(
        IUserAccountRepository userRepository,
        IMemberAccountRepository memberRepository,
        IJwtTokenService jwtTokenService)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _memberRepository = memberRepository ?? throw new ArgumentNullException(nameof(memberRepository));
        _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
    }

    public async Task<AuthResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return new AuthResult(false, ErrorMessage: "Username and password are required.");

        var user = await _userRepository.GetByUsernameAsync(username, cancellationToken);
        if (user == null)
            return new AuthResult(false, ErrorMessage: "Invalid username or password.");

        if (user.Status == UserStatus.PendingActivation)
            return new AuthResult(false, ErrorMessage: "Account is pending activation.");

        if (user.Status == UserStatus.Locked)
            return new AuthResult(false, ErrorMessage: "Account is locked.");

        if (!VerifyPassword(password, user.PasswordHash))
            return new AuthResult(false, ErrorMessage: "Invalid username or password.");

        var token = _jwtTokenService.GenerateToken(user);
        var expiresAt = _jwtTokenService.GetTokenExpiry();

        return new AuthResult(true, token, expiresAt, user);
    }

    public async Task<MemberAuthResult> LoginMemberAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return new MemberAuthResult(false, ErrorMessage: "Username and password are required.");

        var member = await _memberRepository.GetByUsernameAsync(username, cancellationToken);
        if (member == null)
            return new MemberAuthResult(false, ErrorMessage: "Invalid username or password.");

        if (member.Status == MemberAccountStatus.PendingActivation)
            return new MemberAuthResult(false, ErrorMessage: "Account is pending activation.");

        if (member.Status == MemberAccountStatus.Locked)
            return new MemberAuthResult(false, ErrorMessage: "Account is locked.");

        if (!VerifyPassword(password, member.PasswordHash))
            return new MemberAuthResult(false, ErrorMessage: "Invalid username or password.");

        var token = _jwtTokenService.GenerateToken(member);
        var expiresAt = _jwtTokenService.GetTokenExpiry();

        return new MemberAuthResult(true, token, expiresAt, member);
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch
        {
            return false;
        }
    }
}
