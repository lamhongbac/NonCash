using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NonCash.API.DTOs;
using NonCash.Core.Interfaces;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-recovery")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request.Username, request.Password, cancellationToken);

        if (!result.Success)
        {
            if (result.ErrorMessage == "Account is locked.")
                return Forbid(result.ErrorMessage);

            return Unauthorized(new { error = result.ErrorMessage });
        }

        var user = result.User!;
        var response = new LoginResponse(
            result.Token!,
            result.ExpiresAt!.Value,
            new UserDto(user.Id, user.FullName, user.Role.ToString(), user.BrandId, null)
        );

        return Ok(response);
    }

    [HttpPost("member/login")]
    [AllowAnonymous]
    [EnableRateLimiting("member-auth")]
    public async Task<ActionResult<LoginResponse>> MemberLogin(MemberLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginMemberAsync(request.Identifier, request.Password, cancellationToken);

        if (!result.Success)
        {
            if (result.ErrorMessage == "Account is locked.")
                return Forbid(result.ErrorMessage);

            return Unauthorized(new { error = result.ErrorMessage });
        }

        var member = result.Member!;
        var response = new LoginResponse(
            result.Token!,
            result.ExpiresAt!.Value,
            new UserDto(member.Id, member.FullName, "Member", null, member.CustomerId)
        );

        return Ok(response);
    }

    /// <summary>CR-2026-09-09-27: customer self-service recovery. Emails a 30-minute sign-in link to
    /// the account matching a phone number or email. The response is identical whether or not a link
    /// was sent, so this cannot be used to discover who has an account.</summary>
    [HttpPost("member/forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("member-auth")]
    public async Task<ActionResult<MemberForgotPasswordResponse>> MemberForgotPassword(
        MemberForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.MemberForgotPasswordAsync(request.Identifier, cancellationToken);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new MemberForgotPasswordResponse(result.Message!));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-recovery")]
    public async Task<ActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        // Always return success to prevent user enumeration
        await _authService.ForgotPasswordAsync(request.UsernameOrEmail, cancellationToken);
        return Ok(new { message = "If an account with that email or username exists, a password reset email has been sent." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-recovery")]
    public async Task<ActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.ResetPasswordAsync(request.Token, request.NewPassword, cancellationToken);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = "Password has been reset successfully." });
    }

    /// <summary>CR-2026-09-07-18: POS staff login (username + storeCode + password).
    /// Rate-limit this endpoint aggressively to prevent credential stuffing. Generic error
    /// on all failure paths (do not reveal which component was wrong).</summary>
    [HttpPost("staff-login")]
    [AllowAnonymous]
    [EnableRateLimiting("staff-login")]
    public async Task<ActionResult<StaffLoginResponse>> StaffLogin(StaffLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginStaffAsync(request.Username, request.StoreCode, request.Password, cancellationToken);

        if (!result.Success)
        {
            return Unauthorized(new { error = result.ErrorMessage });
        }

        var user = result.User!;
        var response = new StaffLoginResponse(
            result.Token!,
            result.ExpiresAt!.Value,
            new UserDto(user.Id, user.FullName, user.Role.ToString(), user.BrandId, null),
            result.OutletId!.Value,
            result.OutletName ?? string.Empty,
            result.OutletRedemptionMode.ToString()
        );

        return Ok(response);
    }

    /// <summary>CR-2026-09-07-19: Passwordless login via magic-link token from email.
    /// Validates the token and returns a regular session JWT for the member.</summary>
    [HttpPost("magic-link")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-recovery")]
    public async Task<ActionResult<LoginResponse>> MagicLinkLogin(MagicLinkLoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new { error = "Magic link token is required." });

        var result = await _authService.MagicLinkLoginAsync(request.Token, cancellationToken);

        if (!result.Success)
            return Unauthorized(new { error = result.ErrorMessage });

        var member = result.Member!;
        var response = new LoginResponse(
            result.Token!,
            result.ExpiresAt!.Value,
            new UserDto(member.Id, member.FullName, "Member", null, member.CustomerId)
        );

        return Ok(response);
    }

    /// <summary>CR-2026-09-07-19: Set a password for the currently authenticated member.
    /// Optional convenience — lets members skip typing password on future logins.</summary>
    [HttpPost("set-password")]
    [Authorize(Roles = "Member")]
    public async Task<ActionResult> SetMemberPassword(SetMemberPasswordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest(new { error = "Password must be at least 8 characters." });

        // Extract member account ID from JWT claims.
        var subClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        if (subClaim == null || !Guid.TryParse(subClaim, out var memberAccountId))
            return Unauthorized(new { error = "Invalid token." });

        var success = await _authService.SetMemberPasswordAsync(memberAccountId, request.NewPassword, cancellationToken);
        if (!success)
            return BadRequest(new { error = "Failed to set password." });

        return Ok(new { message = "Password has been set successfully. You can now log in with your phone number and this password." });
    }
}
