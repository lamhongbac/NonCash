using Microsoft.AspNetCore.Mvc;
using NonCash.API.DTOs;
using NonCash.Core.Entities;
using NonCash.Core.Services;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/brands")]
public class BrandsController : ControllerBase
{
    private readonly BrandService _brandService;

    public BrandsController(BrandService brandService)
    {
        _brandService = brandService ?? throw new ArgumentNullException(nameof(brandService));
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<BrandResponse>>> GetBrands(
        [FromQuery] string? name,
        [FromQuery] string? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        BrandStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BrandStatus>(status, true, out var parsedStatus))
        {
            statusFilter = parsedStatus;
        }

        var items = await _brandService.ListAsync(name, statusFilter, pageNumber, pageSize, cancellationToken);
        var totalCount = await _brandService.CountAsync(name, statusFilter, cancellationToken);

        var response = new PagedResult<BrandResponse>(
            items.Select(MapToResponse).ToList(),
            totalCount,
            pageNumber,
            pageSize
        );

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BrandResponse>> GetBrand(Guid id, CancellationToken cancellationToken)
    {
        var brand = await _brandService.GetByIdAsync(id, cancellationToken);
        if (brand == null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(brand));
    }

    [HttpPost]
    public async Task<ActionResult<BrandResponse>> CreateBrand(CreateBrandRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var brand = await _brandService.CreateAsync(
                request.Name,
                request.TaxCode,
                request.ContactEmail,
                cancellationToken);

            return CreatedAtAction(nameof(GetBrand), new { id = brand.Id }, MapToResponse(brand));
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
    public async Task<ActionResult<BrandResponse>> UpdateBrand(Guid id, UpdateBrandRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<BrandStatus>(request.Status, true, out var status))
        {
            return BadRequest(new { error = $"Invalid status value '{request.Status}'." });
        }

        try
        {
            var brand = await _brandService.UpdateAsync(id, request.Name, request.ContactEmail, status, cancellationToken);
            return Ok(MapToResponse(brand));
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

    private static BrandResponse MapToResponse(Brand brand)
    {
        return new BrandResponse(
            brand.Id,
            brand.Name,
            brand.TaxCode,
            brand.ContactEmail,
            brand.Status.ToString(),
            brand.CreatedAt,
            brand.UpdatedAt
        );
    }
}
