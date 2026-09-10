namespace NonCash.API.DTOs;

public record LoginRequest(string Username, string Password);

/// <summary>CR-2026-09-09-27: customer sign-in. The identifier is the customer's phone number or
/// email address — the customer contract has no username field.</summary>
public record MemberLoginRequest(string Identifier, string Password);

/// <summary>CR-2026-09-09-27: customer recovery request — a phone number or email address.</summary>
public record MemberForgotPasswordRequest(string Identifier);

/// <summary>CR-2026-09-09-27: neutral confirmation returned by the customer recovery endpoint.</summary>
public record MemberForgotPasswordResponse(string Message);

public record LoginResponse(
    string Token,
    DateTime ExpiresAt,
    UserDto User
);

public record UserDto(
    Guid UserId,
    string FullName,
    string Role,
    Guid? BrandId,
    Guid? CustomerId
);

public record CreateUserRequest(
    string Username,
    string Password,
    string FullName,
    string Role,
    Guid? BrandId,
    string? Email = null
);

public record UpdateUserRequest(
    string FullName,
    string Role,
    Guid? BrandId,
    string? Password,
    string? Email = null
);

public record UserResponse(
    Guid Id,
    string Username,
    string FullName,
    string? Email,
    string Role,
    Guid? BrandId,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record ForgotPasswordRequest(string UsernameOrEmail);

public record ResetPasswordRequest(string Token, string NewPassword);

/// <summary>CR-2026-09-07-19: Passwordless login via magic-link token from email.</summary>
public record MagicLinkLoginRequest(string Token);

/// <summary>CR-2026-09-07-19: Set a password for a member account (optional convenience after magic-link login).</summary>
public record SetMemberPasswordRequest(string NewPassword);
