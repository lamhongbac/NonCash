using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using System.Security.Cryptography;

namespace NonCash.Core.Services;

public class AuthService : IAuthService
{
    private readonly IUserAccountRepository _userRepository;
    private readonly IMemberAccountRepository _memberRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly INotificationService _notificationService;

    public AuthService(
        IUserAccountRepository userRepository,
        IMemberAccountRepository memberRepository,
        IJwtTokenService jwtTokenService,
        INotificationService notificationService)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _memberRepository = memberRepository ?? throw new ArgumentNullException(nameof(memberRepository));
        _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
    }

    public async Task<AuthResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return new AuthResult(false, ErrorMessage: "Username and password are required.");

        username = username.Trim();
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

        username = username.Trim();
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

    public async Task<ForgotPasswordResult> ForgotPasswordAsync(string usernameOrEmail, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(usernameOrEmail))
            return new ForgotPasswordResult(false, "Username or email is required.");

        // Look up by username first, then by email
        var user = await _userRepository.GetByUsernameAsync(usernameOrEmail.Trim(), cancellationToken);
        if (user == null)
        {
            // Try email lookup
            var users = await _userRepository.FindAsync(
                u => u.Email == usernameOrEmail.Trim() && u.Status == UserStatus.Active,
                cancellationToken);
            user = users.FirstOrDefault();
        }

        if (user == null)
        {
            // Don't reveal whether the account exists — return success regardless
            return new ForgotPasswordResult(true);
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            // Cannot send email — return success to avoid revealing account info
            return new ForgotPasswordResult(true);
        }

        // Generate a secure random token
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        user.PasswordResetToken = token;
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(30);
        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync(cancellationToken);

        // Send password reset email
        await _notificationService.NotifyPasswordResetAsync(new PasswordResetNotification(
            user.Email, user.FullName, token, user.PasswordResetTokenExpiry.Value), cancellationToken);

        return new ForgotPasswordResult(true);
    }

    public async Task<AuthResult> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new AuthResult(false, ErrorMessage: "Reset token is required.");

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            return new AuthResult(false, ErrorMessage: "Password must be at least 8 characters.");

        // Find user by reset token
        var users = await _userRepository.FindAsync(
            u => u.PasswordResetToken == token && u.Status == UserStatus.Active,
            cancellationToken);
        var user = users.FirstOrDefault();

        if (user == null)
            return new AuthResult(false, ErrorMessage: "Invalid or expired reset token.");

        if (user.PasswordResetTokenExpiry < DateTime.UtcNow)
        {
            // Token expired — clear it
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;
            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync(cancellationToken);
            return new AuthResult(false, ErrorMessage: "Invalid or expired reset token.");
        }

        // Reset password and clear token
        user.PasswordHash = HashPassword(newPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;
        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return new AuthResult(true);
    }
}
