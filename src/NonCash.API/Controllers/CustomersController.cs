using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public CustomersController(CustomerService customerService, ICustomerImportService importService)
    {
        _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
        _importService = importService ?? throw new ArgumentNullException(nameof(importService));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<CustomerResponse>>> GetCustomers(
        [FromQuery] string? phoneNumber,
        [FromQuery] string? name,
        [FromQuery] string? email,
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

        var (items, totalCount) = await _customerService.SearchAsync(
            phoneNumber, name, email, statusFilter, pageNumber, pageSize, cancellationToken);

        var response = new PagedResult<CustomerResponse>(
            items.Select(MapToResponse).ToList(),
            totalCount,
            pageNumber,
            pageSize
        );

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<CustomerResponse>> GetCustomer(Guid id, CancellationToken cancellationToken)
    {
        var customer = await _customerService.GetByIdAsync(id, cancellationToken);
        if (customer == null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(customer));
    }

    [HttpPost]
    public async Task<ActionResult<CustomerResponse>> CreateCustomer(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var customer = await _customerService.CreateAsync(
                request.PhoneNumber,
                request.FullName,
                request.Email,
                cancellationToken);

            return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, MapToResponse(customer));
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
            var customer = await _customerService.UpdateAsync(id, request.FullName, request.Email, cancellationToken);
            return Ok(MapToResponse(customer));
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

    [HttpPut("{id:guid}/blacklist")]
    [Authorize(Roles = "BrandManager,Admin")]
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
    [Authorize(Roles = "BrandManager,Admin")]
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

    [HttpPost("import")]
    public async Task<ActionResult<CustomerImportResponse>> ImportCustomers(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file uploaded." });
        }

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Only CSV files are supported." });
        }

        try
        {
            using var stream = file.OpenReadStream();
            var result = await _importService.ImportFromCsvAsync(stream, cancellationToken);

            return Ok(new CustomerImportResponse(
                result.Created,
                result.Updated,
                result.Errors.Count,
                result.Errors
            ));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Import failed: {ex.Message}" });
        }
    }

    private static CustomerResponse MapToResponse(Customer customer)
    {
        return new CustomerResponse(
            customer.Id,
            customer.PhoneNumber,
            customer.FullName,
            customer.Email,
            customer.Status.ToString(),
            customer.CreatedAt,
            customer.UpdatedAt
        );
    }
}
