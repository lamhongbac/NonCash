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
    private readonly ICustomerRepository _customerRepository;
    private readonly IOutletRepository _outletRepository;
    private readonly IUserOutletRepository _userOutletRepository;

    public AuthService(
        IUserAccountRepository userRepository,
        IMemberAccountRepository memberRepository,
        IJwtTokenService jwtTokenService,
        INotificationService notificationService,
        ICustomerRepository customerRepository,
        IOutletRepository outletRepository,
        IUserOutletRepository userOutletRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _memberRepository = memberRepository ?? throw new ArgumentNullException(nameof(memberRepository));
        _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _outletRepository = outletRepository ?? throw new ArgumentNullException(nameof(outletRepository));
        _userOutletRepository = userOutletRepository ?? throw new ArgumentNullException(nameof(userOutletRepository));
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

        // Matrix row 10 (O2): platform-blacklisted customers cannot log in. The customer's
        // Status is read directly (NOT mirrored into MemberAccount.Status) so an
        // un-blacklist restores login instantly (P4 reversibility).
        var memberCustomer = await _customerRepository.GetByIdAsync(member.CustomerId, cancellationToken);
        if (memberCustomer?.Status == CustomerStatus.Blacklisted)
            return new MemberAuthResult(false, ErrorMessage: "Account is locked.");

        if (!VerifyPassword(password, member.PasswordHash))
            return new MemberAuthResult(false, ErrorMessage: "Invalid username or password.");

        var token = _jwtTokenService.GenerateToken(member);
        var expiresAt = _jwtTokenService.GetTokenExpiry();

        return new MemberAuthResult(true, token, expiresAt, member);
    }

    public async Task<StaffAuthResult> LoginStaffAsync(string username, string storeCode, string password, CancellationToken cancellationToken = default)
    {
        // Generic failure message for all error paths (CR-2026-09-07-18: do not reveal which
        // component is wrong — username, store code, password, or assignment).
        const string GenericFailure = "Invalid credentials.";

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(storeCode) || string.IsNullOrWhiteSpace(password))
            return new StaffAuthResult(false, ErrorMessage: GenericFailure);

        username = username.Trim();
        storeCode = storeCode.Trim();

        var user = await _userRepository.GetByUsernameAsync(username, cancellationToken);
        if (user == null || user.Role != UserRole.StoreStaff || user.Status != UserStatus.Active || user.BrandId == null)
            return new StaffAuthResult(false, ErrorMessage: GenericFailure);

        if (!VerifyPassword(password, user.PasswordHash))
            return new StaffAuthResult(false, ErrorMessage: GenericFailure);

        var outlet = await _outletRepository.GetByCodeAsync(user.BrandId.Value, storeCode, cancellationToken);
        if (outlet == null)
            return new StaffAuthResult(false, ErrorMessage: GenericFailure);

        // Verify this staff is assigned to this outlet.
        var assignments = await _userOutletRepository.GetOutletsForUserAsync(user.Id, cancellationToken);
        if (!assignments.Any(o => o.Id == outlet.Id))
            return new StaffAuthResult(false, ErrorMessage: GenericFailure);

        var token = _jwtTokenService.GenerateToken(user, outlet.Id);
        var expiresAt = _jwtTokenService.GetTokenExpiry();

        return new StaffAuthResult(true, token, expiresAt, user, outlet.Id);
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

    /// <summary>CR-2026-09-07-19: Passwordless login via magic-link token.</summary>
    public async Task<MemberAuthResult> MagicLinkLoginAsync(string magicLinkToken, CancellationToken cancellationToken = default)
    {
        var memberAccountId = _jwtTokenService.ValidateMagicLinkToken(magicLinkToken);
        if (memberAccountId == null)
            return new MemberAuthResult(false, ErrorMessage: "Invalid or expired magic link.");

        var member = await _memberRepository.GetByIdAsync(memberAccountId.Value, cancellationToken);
        if (member == null)
            return new MemberAuthResult(false, ErrorMessage: "Account not found.");

        if (member.Status == MemberAccountStatus.Locked)
            return new MemberAuthResult(false, ErrorMessage: "Account is locked.");

        // Check blacklist (same as LoginMemberAsync)
        var memberCustomer = await _customerRepository.GetByIdAsync(member.CustomerId, cancellationToken);
        if (memberCustomer?.Status == CustomerStatus.Blacklisted)
            return new MemberAuthResult(false, ErrorMessage: "Account is locked.");

        var token = _jwtTokenService.GenerateToken(member);
        var expiresAt = _jwtTokenService.GetTokenExpiry();

        return new MemberAuthResult(true, token, expiresAt, member);
    }

    /// <summary>CR-2026-09-07-19: Set password for a member account (optional convenience).</summary>
    public async Task<bool> SetMemberPasswordAsync(Guid memberAccountId, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            return false;

        var member = await _memberRepository.GetByIdAsync(memberAccountId, cancellationToken);
        if (member == null)
            return false;

        member.PasswordHash = HashPassword(newPassword);
        member.Username = member.Username; // keep existing username
        _memberRepository.Update(member);
        await _memberRepository.SaveChangesAsync(cancellationToken);
        return true;
    }
}
