using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.API.DTOs;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;

namespace NonCash.API.Controllers;

/// <summary>
/// CRUD for StoreStaff users (CR-2026-09-07-18). BrandManager can CRUD StoreStaff within
/// their own brand only (BrandId is read from the JWT, never trusted from the request).
/// Admin has cross-brand visibility.
/// </summary>
[ApiController]
[Route("api/v1/store-staff")]
[Authorize(Roles = "Admin,BrandManager")]
public class StoreStaffController : ControllerBase
{
    private readonly StoreStaffService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IOutletRepository _outletRepository;

    public StoreStaffController(
        StoreStaffService service,
        ICurrentUserService currentUser,
        IOutletRepository outletRepository)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _outletRepository = outletRepository ?? throw new ArgumentNullException(nameof(outletRepository));
    }

    private (Guid BrandId, ActionResult? Error) ResolveBrandScope()
    {
        if (_currentUser.IsInRole("BrandManager"))
        {
            var brandId = _currentUser.GetCurrentBrandId();
            if (brandId == null)
                return (Guid.Empty, Unauthorized(new { error = "Invalid user context." }));
            return (brandId.Value, null);
        }
        // Admin: brandId comes from the query parameter (required)
        return (Guid.Empty, null);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<StoreStaffResponse>>> List([FromQuery] Guid? brandId, CancellationToken cancellationToken)
    {
        var (scope, error) = await ResolveListScope(brandId, cancellationToken);
        if (error != null) return error;

        var users = await _service.ListAsync(scope, cancellationToken);
        var results = new List<StoreStaffResponse>();
        foreach (var user in users)
        {
            var detail = await _service.GetByIdAsync(scope, user.Id, cancellationToken);
            if (detail == null) continue;
            results.Add(MapToResponse(detail.Value.User, detail.Value.Outlets));
        }
        return Ok(results);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StoreStaffResponse>> Get(Guid id, [FromQuery] Guid? brandId, CancellationToken cancellationToken)
    {
        var (scope, error) = await ResolveListScope(brandId, cancellationToken);
        if (error != null) return error;

        var detail = await _service.GetByIdAsync(scope, id, cancellationToken);
        if (detail == null) return NotFound();

        return Ok(MapToResponse(detail.Value.User, detail.Value.Outlets));
    }

    [HttpPost]
    public async Task<ActionResult<StoreStaffResponse>> Create(CreateStoreStaffRequest request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsInRole("BrandManager"))
        {
            return Forbid(); // Admin should not create StoreStaff directly (use UsersController)
        }

        var brandId = _currentUser.GetCurrentBrandId();
        if (brandId == null)
            return Unauthorized(new { error = "Invalid user context." });

        try
        {
            var user = await _service.CreateAsync(
                brandId.Value,
                request.Username,
                request.Password,
                request.FullName,
                request.Email,
                request.OutletIds,
                cancellationToken);

            var detail = await _service.GetByIdAsync(brandId.Value, user.Id, cancellationToken);
            var outlets = detail?.Outlets ?? Enumerable.Empty<Outlet>();
            return CreatedAtAction(nameof(Get), new { id = user.Id }, MapToResponse(user, outlets));
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

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StoreStaffResponse>> Update(Guid id, UpdateStoreStaffRequest request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsInRole("BrandManager"))
            return Forbid();

        var brandId = _currentUser.GetCurrentBrandId();
        if (brandId == null)
            return Unauthorized(new { error = "Invalid user context." });

        try
        {
            var user = await _service.UpdateAsync(
                brandId.Value, id, request.FullName, request.Email, request.Password, request.OutletIds, cancellationToken);

            var detail = await _service.GetByIdAsync(brandId.Value, user.Id, cancellationToken);
            var outlets = detail?.Outlets ?? Enumerable.Empty<Outlet>();
            return Ok(MapToResponse(user, outlets));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("{id:guid}/lock")]
    public async Task<ActionResult<StoreStaffResponse>> Lock(Guid id, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsInRole("BrandManager"))
            return Forbid();

        var brandId = _currentUser.GetCurrentBrandId();
        if (brandId == null)
            return Unauthorized(new { error = "Invalid user context." });

        try
        {
            var user = await _service.LockAsync(brandId.Value, id, cancellationToken);
            var detail = await _service.GetByIdAsync(brandId.Value, user.Id, cancellationToken);
            var outlets = detail?.Outlets ?? Enumerable.Empty<Outlet>();
            return Ok(MapToResponse(user, outlets));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("{id:guid}/unlock")]
    public async Task<ActionResult<StoreStaffResponse>> Unlock(Guid id, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsInRole("BrandManager"))
            return Forbid();

        var brandId = _currentUser.GetCurrentBrandId();
        if (brandId == null)
            return Unauthorized(new { error = "Invalid user context." });

        try
        {
            var user = await _service.UnlockAsync(brandId.Value, id, cancellationToken);
            var detail = await _service.GetByIdAsync(brandId.Value, user.Id, cancellationToken);
            var outlets = detail?.Outlets ?? Enumerable.Empty<Outlet>();
            return Ok(MapToResponse(user, outlets));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsInRole("BrandManager"))
            return Forbid();

        var brandId = _currentUser.GetCurrentBrandId();
        if (brandId == null)
            return Unauthorized(new { error = "Invalid user context." });

        try
        {
            await _service.DeleteAsync(brandId.Value, id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private async Task<(Guid BrandId, ActionResult? Error)> ResolveListScope(Guid? brandId, CancellationToken cancellationToken)
    {
        if (_currentUser.IsInRole("BrandManager"))
        {
            var id = _currentUser.GetCurrentBrandId();
            if (id == null)
                return (Guid.Empty, Unauthorized(new { error = "Invalid user context." }));
            return (id.Value, null);
        }

        // Admin: brandId required
        if (brandId == null || brandId == Guid.Empty)
            return (Guid.Empty, BadRequest(new { error = "brandId query parameter is required for Admin." }));

        await Task.CompletedTask;
        return (brandId.Value, null);
    }

    private static StoreStaffResponse MapToResponse(UserAccount user, IEnumerable<Outlet> outlets)
    {
        return new StoreStaffResponse(
            user.Id,
            user.Username,
            user.FullName,
            user.Email,
            user.Status.ToString(),
            outlets.Select(o => new StoreStaffOutletDto(o.Id, o.Code, o.Name)).ToList(),
            user.CreatedAt,
            user.UpdatedAt
        );
    }
}
