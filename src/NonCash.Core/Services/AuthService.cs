using Microsoft.Extensions.Configuration;
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
    private readonly IConfiguration _configuration;

    public AuthService(
        IUserAccountRepository userRepository,
        IMemberAccountRepository memberRepository,
        IJwtTokenService jwtTokenService,
        INotificationService notificationService,
        ICustomerRepository customerRepository,
        IOutletRepository outletRepository,
        IUserOutletRepository userOutletRepository,
        IConfiguration configuration)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _memberRepository = memberRepository ?? throw new ArgumentNullException(nameof(memberRepository));
        _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _outletRepository = outletRepository ?? throw new ArgumentNullException(nameof(outletRepository));
        _userOutletRepository = userOutletRepository ?? throw new ArgumentNullException(nameof(userOutletRepository));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>Customer-facing explanation for an account the system provisioned on their behalf
    /// (voucher distribution or gifting) before they ever chose a password.</summary>
    public const string NoPasswordYetMessage =
        "Your account was created automatically when a brand sent you a voucher, so it has no password yet. "
        + "Choose Forgot password on the sign-in screen and we will email you a link to set one, "
        + "or open that voucher email and use its sign-in link. "
        + "If we have no email address for you, contact the brand that sent the voucher.";

    /// <summary>The identifier is neither an email address nor a plausible phone number.</summary>
    public const string MalformedIdentifierMessage =
        "Enter the phone number or email address on your account — for example 0913660575 or you@example.com.";

    /// <summary>Generic failure: the identifier is well formed but does not match an account, or the
    /// password is wrong. Kept identical for both so login cannot be used to enumerate customers.</summary>
    public const string InvalidIdentifierMessage = "Invalid phone number/email or password.";

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

    /// <summary>Customer sign-in. CR-2026-09-09-27: customers have no username. The identifier is
    /// their phone number or their email address — both unique on the global customer record and
    /// both already known to the customer.</summary>
    public async Task<MemberAuthResult> LoginMemberAsync(string identifier, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
            return new MemberAuthResult(false, ErrorMessage: "Enter your phone number or email, and your password.");

        var member = await FindMemberByIdentifierAsync(identifier, cancellationToken);
        if (member == null)
        {
            // A malformed identifier is an input problem, not a secret: telling the customer the
            // shape we expect costs nothing and saves them a retry loop.
            return new MemberAuthResult(false, ErrorMessage:
                ClassifyIdentifier(identifier.Trim()) == IdentifierShape.Invalid
                    ? MalformedIdentifierMessage
                    : InvalidIdentifierMessage);
        }

        if (member.Status == MemberAccountStatus.PendingActivation)
            return new MemberAuthResult(false, ErrorMessage:
                "Your account has not been activated yet. Contact the brand that sent you the voucher and ask them to activate it, then sign in again.");

        if (member.Status == MemberAccountStatus.Locked)
            return new MemberAuthResult(false, ErrorMessage: "Account is locked.");

        // Matrix row 10 (O2): platform-blacklisted customers cannot log in. The customer's
        // Status is read directly (NOT mirrored into MemberAccount.Status) so an
        // un-blacklist restores login instantly (P4 reversibility).
        var memberCustomer = await _customerRepository.GetByIdAsync(member.CustomerId, cancellationToken);
        if (memberCustomer?.Status == CustomerStatus.Blacklisted)
            return new MemberAuthResult(false, ErrorMessage: "Account is locked.");

        // Accounts provisioned by voucher distribution/gifting carry an empty hash: the customer
        // never chose a password. Reporting "invalid password" here sends them in a circle.
        if (string.IsNullOrEmpty(member.PasswordHash))
            return new MemberAuthResult(false, ErrorMessage: NoPasswordYetMessage);

        if (!VerifyPassword(password, member.PasswordHash))
            return new MemberAuthResult(false, ErrorMessage: InvalidIdentifierMessage);

        var token = _jwtTokenService.GenerateToken(member);
        var expiresAt = _jwtTokenService.GetTokenExpiry();

        return new MemberAuthResult(true, token, expiresAt, member);
    }

    private enum IdentifierShape { Phone, Email, Invalid }

    /// <summary>An '@' means email; otherwise 9–15 digits after normalization means phone.
    /// Anything else cannot match a customer, so callers can say what shape is expected.</summary>
    private static IdentifierShape ClassifyIdentifier(string identifier)
    {
        if (identifier.Contains('@'))
            return IdentifierShape.Email;

        return Customer.NormalizePhoneNumber(identifier).Length is >= 9 and <= 15
            ? IdentifierShape.Phone
            : IdentifierShape.Invalid;
    }

    /// <summary>Resolves a member account from the customer's phone number or email address.
    /// Returns null when the identifier is malformed, unknown, or has no member account yet.</summary>
    private async Task<MemberAccount?> FindMemberByIdentifierAsync(string identifier, CancellationToken cancellationToken)
    {
        identifier = identifier.Trim();

        var customer = ClassifyIdentifier(identifier) switch
        {
            IdentifierShape.Email => await _customerRepository.GetByEmailAsync(
                Customer.NormalizeEmail(identifier)!, cancellationToken),
            IdentifierShape.Phone => await _customerRepository.GetByPhoneNumberAsync(
                Customer.NormalizePhoneNumber(identifier), cancellationToken),
            _ => null
        };

        if (customer == null)
            return null;

        return await _memberRepository.GetByCustomerIdAsync(customer.Id, cancellationToken);
    }

    /// <summary>How long an emailed recovery link stays usable.</summary>
    public static readonly TimeSpan SignInLinkLifetime = TimeSpan.FromMinutes(30);

    /// <summary>Customer-facing confirmation. Deliberately identical whether or not a link was
    /// actually sent, so this endpoint cannot be used to discover who has an account.</summary>
    public const string SignInLinkSentMessage =
        "If an account matches that phone number or email, we have sent a sign-in link. "
        + "It expires in 30 minutes. Check your inbox and spam folder; "
        + "if nothing arrives, we have no email address for you — contact the brand that sent your voucher.";

    /// <summary>CR-2026-09-09-27: customer self-service recovery. Closes the dead end where the
    /// voucher magic link had expired and the customer had never chosen a password.</summary>
    public async Task<MemberSignInLinkResult> MemberForgotPasswordAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return new MemberSignInLinkResult(false, ErrorMessage: MalformedIdentifierMessage);

        identifier = identifier.Trim();
        if (ClassifyIdentifier(identifier) == IdentifierShape.Invalid)
            return new MemberSignInLinkResult(false, ErrorMessage: MalformedIdentifierMessage);

        var member = await FindMemberByIdentifierAsync(identifier, cancellationToken);
        if (member == null)
            return new MemberSignInLinkResult(true, SignInLinkSentMessage);

        var customer = await _customerRepository.GetByIdAsync(member.CustomerId, cancellationToken);

        // No delivery channel, or matrix row 10 (O2) says this customer may not get in — either
        // way the answer stays neutral rather than explaining which one it was.
        if (customer == null
            || string.IsNullOrWhiteSpace(customer.Email)
            || customer.Status == CustomerStatus.Blacklisted)
        {
            return new MemberSignInLinkResult(true, SignInLinkSentMessage);
        }

        var token = _jwtTokenService.GenerateSignInLinkToken(member.Id);
        var webBaseUrl = _configuration["WebBaseUrl"]?.TrimEnd('/') ?? "https://localhost:7162";
        var signInLinkUrl = $"{webBaseUrl}/member/welcome?token={Uri.EscapeDataString(token)}";

        await _notificationService.NotifySignInLinkAsync(new SignInLinkNotification(
            customer.Email,
            string.IsNullOrWhiteSpace(member.FullName) ? customer.FullName : member.FullName,
            signInLinkUrl,
            DateTime.UtcNow.Add(SignInLinkLifetime)), cancellationToken);

        return new MemberSignInLinkResult(true, SignInLinkSentMessage);
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

        return new StaffAuthResult(true, token, expiresAt, user, outlet.Id, outlet.Name, outlet.PosRedemptionMode);
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
        _memberRepository.Update(member);
        await _memberRepository.SaveChangesAsync(cancellationToken);
        return true;
    }
}
