using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<ActionResult<LoginResponse>> MemberLogin(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginMemberAsync(request.Username, request.Password, cancellationToken);

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

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        // Always return success to prevent user enumeration
        await _authService.ForgotPasswordAsync(request.UsernameOrEmail, cancellationToken);
        return Ok(new { message = "If an account with that email or username exists, a password reset email has been sent." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<ActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.ResetPasswordAsync(request.Token, request.NewPassword, cancellationToken);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = "Password has been reset successfully." });
    }
}
