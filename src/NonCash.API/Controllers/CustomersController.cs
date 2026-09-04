using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NonCash.API.DTOs;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/customers")]
[Authorize(Roles = "BrandManager,Admin")]
public class CustomersController : ControllerBase
{
    private readonly CustomerService _customerService;
    private readonly ICustomerImportService _importService;
    private readonly ICurrentUserService _currentUser;

    public CustomersController(CustomerService customerService, ICustomerImportService importService, ICurrentUserService currentUser)
    {
        _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
        _importService = importService ?? throw new ArgumentNullException(nameof(importService));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    /// <summary>
    /// BrandManager requests are scoped to their own brand (mapping-scoped visibility);
    /// Admin requests see all brands. Returns an error action when a BrandManager
    /// has no valid brand context.
    /// </summary>
    private (Guid? BrandId, ActionResult? Error) ResolveBrandScope()
    {
        if (_currentUser.IsInRole("BrandManager"))
        {
            var brandId = _currentUser.GetCurrentBrandId();
            if (brandId == null)
                return (null, Unauthorized(new { error = "Invalid user context." }));
            return (brandId, null);
        }

        return (null, null); // Admin: all brands
    }

    /// <summary>Parses the current user's id into a Guid for CreatedBy audit columns (null-safe).</summary>
    private Guid? ParseCurrentUserId()
    {
        return Guid.TryParse(_currentUser.GetCurrentUserId(), out var userId) ? userId : null;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<CustomerResponse>>> GetCustomers(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        CustomerStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CustomerStatus>(status, true, out var parsedStatus))
        {
            statusFilter = parsedStatus;
        }

        var (brandScope, scopeError) = ResolveBrandScope();
        if (scopeError != null)
            return scopeError;

        try
        {
            var (items, totalCount) = await _customerService.SearchAsync(
                search, statusFilter, pageNumber, pageSize, cancellationToken, brandScope);

            // When brand-scoped, expose the per-brand block flag (matrix S1) for each row.
            var mappingsByCustomerId = new Dictionary<Guid, BrandCustomer>();
            if (brandScope.HasValue)
            {
                var mappings = await _customerService.GetBrandMappingsAsync(
                    brandScope.Value, items.Select(c => c.Id), cancellationToken);
                foreach (var mapping in mappings)
                    mappingsByCustomerId[mapping.CustomerId] = mapping;
            }

            var response = new PagedResult<CustomerResponse>(
                items.Select(c => MapToResponse(c, mappingsByCustomerId.GetValueOrDefault(c.Id))).ToList(),
                totalCount,
                pageNumber,
                pageSize
            );

            return Ok(response);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return SchemaOutdated();
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerResponse>> GetCustomer(Guid id, CancellationToken cancellationToken)
    {
        var (brandScope, scopeError) = ResolveBrandScope();
        if (scopeError != null)
            return scopeError;

        try
        {
            var customer = await _customerService.GetByIdAsync(id, cancellationToken, brandScope);
            if (customer == null)
            {
                return NotFound(); // Unknown id OR not mapped to the caller's brand
            }

            BrandCustomer? mapping = null;
            if (brandScope.HasValue)
                mapping = await _customerService.GetBrandMappingAsync(brandScope.Value, id, cancellationToken);

            return Ok(MapToResponse(customer, mapping));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return SchemaOutdated();
        }
    }

    [HttpPost]
    public async Task<ActionResult<CustomerResponse>> CreateCustomer(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var (brandScope, scopeError) = ResolveBrandScope();
            if (scopeError != null)
                return scopeError;

            var customer = await _customerService.CreateAsync(
                request.PhoneNumber,
                request.FullName,
                request.Email,
                cancellationToken,
                brandScope,
                ParseCurrentUserId());

            BrandCustomer? mapping = null;
            if (brandScope.HasValue)
                mapping = await _customerService.GetBrandMappingAsync(brandScope.Value, customer.Id, cancellationToken);

            return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, MapToResponse(customer, mapping));
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
    public async Task<ActionResult<CustomerResponse>> UpdateCustomer(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var (brandScope, scopeError) = ResolveBrandScope();
            if (scopeError != null)
                return scopeError;

            var customer = await _customerService.UpdateAsync(id, request.FullName, request.Email, cancellationToken, brandScope);

            BrandCustomer? mapping = null;
            if (brandScope.HasValue)
                mapping = await _customerService.GetBrandMappingAsync(brandScope.Value, id, cancellationToken);

            return Ok(MapToResponse(customer, mapping));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
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

    // Platform blacklist (matrix S2): Admin-only. A BrandManager blocks a customer
    // for their own brand via the /block endpoint below instead.
    [HttpPut("{id:guid}/blacklist")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CustomerResponse>> BlacklistCustomer(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var customer = await _customerService.BlacklistAsync(id, cancellationToken);
            return Ok(MapToResponse(customer));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("{id:guid}/unblacklist")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CustomerResponse>> UnblacklistCustomer(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var customer = await _customerService.UnblacklistAsync(id, cancellationToken);
            return Ok(MapToResponse(customer));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Per-brand block (matrix S1, P2/P3): the customer stops receiving this brand's
    /// promotions, buying this brand's plans and being gifted this brand's vouchers.
    /// BrandManager → own brand; Admin → must pass ?brandId=.
    /// </summary>
    [HttpPut("{id:guid}/block")]
    public async Task<ActionResult<CustomerResponse>> BlockCustomer(Guid id, [FromQuery] Guid? brandId, CancellationToken cancellationToken)
    {
        return await SetBrandBlocked(id, brandId, blocked: true, cancellationToken);
    }

    [HttpPut("{id:guid}/unblock")]
    public async Task<ActionResult<CustomerResponse>> UnblockCustomer(Guid id, [FromQuery] Guid? brandId, CancellationToken cancellationToken)
    {
        return await SetBrandBlocked(id, brandId, blocked: false, cancellationToken);
    }

    private async Task<ActionResult<CustomerResponse>> SetBrandBlocked(Guid id, Guid? brandIdQuery, bool blocked, CancellationToken cancellationToken)
    {
        Guid brandId;
        if (_currentUser.IsInRole("BrandManager"))
        {
            var ownBrandId = _currentUser.GetCurrentBrandId();
            if (ownBrandId == null)
                return Unauthorized(new { error = "Invalid user context." });
            brandId = ownBrandId.Value;
        }
        else // Admin must name the brand explicitly
        {
            if (brandIdQuery == null || brandIdQuery == Guid.Empty)
                return BadRequest(new { error = "The brandId query parameter is required." });
            brandId = brandIdQuery.Value;
        }

        try
        {
            if (blocked)
                await _customerService.BlockAsync(brandId, id, cancellationToken);
            else
                await _customerService.UnblockAsync(brandId, id, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(); // Customer is not mapped to this brand
        }

        var customer = await _customerService.GetByIdAsync(id, cancellationToken);
        if (customer == null)
            return NotFound();

        var mapping = await _customerService.GetBrandMappingAsync(brandId, id, cancellationToken);
        return Ok(MapToResponse(customer, mapping));
    }

    [HttpPost("import")]
    public async Task<ActionResult<CustomerImportResponse>> ImportCustomers(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file uploaded." });
        }

        try
        {
            var (brandScope, scopeError) = ResolveBrandScope();
            if (scopeError != null)
                return scopeError;

            using var stream = file.OpenReadStream();
            var result = await _importService.ImportAsync(stream, cancellationToken, brandScope, ParseCurrentUserId());

            return Ok(new CustomerImportResponse(
                result.Created,
                result.Updated,
                result.Errors.Count,
                result.Errors.Select(e => new CustomerImportErrorDto(e.Row, e.PhoneNumber, e.FullName, e.Email, e.Message)).ToList()
            ));
        }
        catch (CustomerImportParseException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Import failed: {ex.Message}" });
        }
    }

    /// <summary>
    /// Clear 503 when the brand_customers table is missing (migration not applied)
    /// instead of an opaque 500.
    /// </summary>
    private ActionResult SchemaOutdated()
    {
        return StatusCode(StatusCodes.Status503ServiceUnavailable,
            new { error = "Database schema is out of date: the brand_customers migration has not been applied." });
    }

    private static CustomerResponse MapToResponse(Customer customer, BrandCustomer? mapping = null)
    {
        return new CustomerResponse(
            customer.Id,
            customer.PhoneNumber,
            customer.FullName,
            customer.Email,
            customer.Status.ToString(),
            customer.CreatedAt,
            customer.UpdatedAt,
            mapping?.IsBlocked
        );
    }
}
