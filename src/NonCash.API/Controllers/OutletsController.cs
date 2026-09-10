using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.API.DTOs;
using NonCash.Core.Entities;
using NonCash.Core.Services;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/outlets")]
[Authorize(Roles = "BrandManager,Admin")]
public class OutletsController : ControllerBase
{
    private readonly OutletService _outletService;

    public OutletsController(OutletService outletService)
    {
        _outletService = outletService ?? throw new ArgumentNullException(nameof(outletService));
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<OutletResponse>>> GetOutlets(
        [FromQuery] string? name,
        [FromQuery] string? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        OutletStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OutletStatus>(status, true, out var parsedStatus))
        {
            statusFilter = parsedStatus;
        }

        var (items, totalCount) = await _outletService.ListByBrandAsync(
            name, statusFilter, pageNumber, pageSize, cancellationToken);

        var response = new PagedResult<OutletResponse>(
            items.Select(MapToResponse).ToList(),
            totalCount,
            pageNumber,
            pageSize
        );

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OutletResponse>> GetOutlet(Guid id, CancellationToken cancellationToken)
    {
        var outlet = await _outletService.GetByIdAsync(id, cancellationToken);
        if (outlet == null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(outlet));
    }

    [HttpPost]
    public async Task<ActionResult<OutletResponse>> CreateOutlet(CreateOutletRequest request, CancellationToken cancellationToken)
    {
        if (!TryParseMode(request.RedemptionMode, out var mode, out var modeError))
        {
            return BadRequest(new { error = modeError });
        }

        try
        {
            var outlet = await _outletService.CreateAsync(
                request.Name,
                request.Address,
                request.Code,
                mode ?? PosRedemptionMode.OneClick,
                cancellationToken);

            return CreatedAtAction(nameof(GetOutlet), new { id = outlet.Id }, MapToResponse(outlet));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OutletResponse>> UpdateOutlet(Guid id, UpdateOutletRequest request, CancellationToken cancellationToken)
    {
        if (!TryParseMode(request.RedemptionMode, out var mode, out var modeError))
        {
            return BadRequest(new { error = modeError });
        }

        try
        {
            var outlet = await _outletService.UpdateAsync(id, request.Name, request.Address, request.Code, mode, cancellationToken);
            return Ok(MapToResponse(outlet));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/close")]
    public async Task<ActionResult<OutletResponse>> CloseOutlet(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var outlet = await _outletService.CloseAsync(id, cancellationToken);
            return Ok(MapToResponse(outlet));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private static OutletResponse MapToResponse(Outlet outlet)
    {
        return new OutletResponse(
            outlet.Id,
            outlet.BrandId,
            outlet.Name,
            outlet.Address,
            outlet.Code,
            outlet.Status.ToString(),
            outlet.ApiKeyPrefix,
            outlet.PosRedemptionMode.ToString(),
            outlet.CreatedAt,
            outlet.UpdatedAt
        );
    }

    private static bool TryParseMode(string? value, out PosRedemptionMode? mode, out string? error)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            mode = null;
            error = null;
            return true;
        }

        if (Enum.TryParse<PosRedemptionMode>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            mode = parsed;
            error = null;
            return true;
        }

        var allowed = string.Join(", ", Enum.GetNames<PosRedemptionMode>());
        error = $"Unknown redemption mode '{value}'. Allowed values: {allowed}.";
        mode = null;
        return false;
    }
}
