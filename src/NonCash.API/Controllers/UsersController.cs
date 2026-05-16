using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.API.DTOs;
using NonCash.Core.Entities;
using NonCash.Core.Services;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;

    public UsersController(UserService userService)
    {
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserResponse>>> GetUsers(
        [FromQuery] Guid? brandId,
        CancellationToken cancellationToken)
    {
        var users = await _userService.ListAsync(brandId, cancellationToken);
        return Ok(users.Select(MapToResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponse>> GetUser(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userService.GetByIdAsync(id, cancellationToken);
        if (user == null) return NotFound();

        return Ok(MapToResponse(user));
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> CreateUser(CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
        {
            return BadRequest(new { error = $"Invalid role '{request.Role}'. Valid roles: Admin, BrandManager, Planner, Approver" });
        }

        try
        {
            var user = await _userService.CreateAsync(
                request.Username, request.Password, request.FullName, role, request.BrandId, cancellationToken);

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, MapToResponse(user));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/lock")]
    public async Task<ActionResult<UserResponse>> LockUser(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userService.LockAsync(id, cancellationToken);
            return Ok(MapToResponse(user));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("{id:guid}/unlock")]
    public async Task<ActionResult<UserResponse>> UnlockUser(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userService.UnlockAsync(id, cancellationToken);
            return Ok(MapToResponse(user));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private static UserResponse MapToResponse(UserAccount user)
    {
        return new UserResponse(
            user.Id,
            user.Username,
            user.FullName,
            user.Role.ToString(),
            user.BrandId,
            user.Status.ToString(),
            user.CreatedAt,
            user.UpdatedAt
        );
    }
}
