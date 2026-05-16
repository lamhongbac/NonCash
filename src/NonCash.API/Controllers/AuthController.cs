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
            new UserDto(user.Id, user.FullName, user.Role.ToString(), user.BrandId)
        );

        return Ok(response);
    }
}
