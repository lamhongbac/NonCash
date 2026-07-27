using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.Core.Interfaces;

namespace NonCash.API.Controllers;

/// <summary>
/// Admin-only controller for managing integration partners (CRUD, key generation, brand associations).
/// </summary>
[ApiController]
[Route("api/v1/integration-partners")]
[Authorize]
public class IntegrationPartnersController : ControllerBase
{
    private readonly IIntegrationPartnerService _partnerService;

    public IntegrationPartnersController(IIntegrationPartnerService partnerService)
    {
        _partnerService = partnerService;
    }

    [HttpGet]
    public async Task<ActionResult> List(CancellationToken cancellationToken)
    {
        var partners = await _partnerService.ListAsync(cancellationToken);
        var response = partners.Select(p => new PartnerDto(
            p.Id, p.Name, p.ContactEmail, p.CallbackUrl,
            p.ApiKeyPrefix, p.IsActive, p.CreatedAt,
            p.PartnerBrands.Select(pb => new PartnerBrandDto(pb.BrandId, pb.Brand?.Name)).ToList()
        )).ToList();
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var partner = await _partnerService.GetByIdAsync(id, cancellationToken);
        if (partner == null) return NotFound();

        return Ok(new PartnerDto(
            partner.Id, partner.Name, partner.ContactEmail, partner.CallbackUrl,
            partner.ApiKeyPrefix, partner.IsActive, partner.CreatedAt,
            partner.PartnerBrands.Select(pb => new PartnerBrandDto(pb.BrandId, pb.Brand?.Name)).ToList()
        ));
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreatePartnerRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.ContactEmail))
            return BadRequest(new { error = "Name and ContactEmail are required." });

        var partner = await _partnerService.CreateAsync(
            request.Name, request.ContactEmail, request.CallbackUrl, request.BrandIds ?? new(), cancellationToken);

        return Ok(new { partner.Id, partner.ApiKeyPrefix, Message = "Partner created. Use GenerateKey to obtain the full API key." });
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdatePartnerRequest request, CancellationToken cancellationToken)
    {
        var partner = await _partnerService.UpdateAsync(id, request.Name, request.ContactEmail, request.CallbackUrl, request.IsActive, cancellationToken);
        if (partner == null) return NotFound();
        return Ok(new { partner.Id, partner.Name, partner.IsActive });
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var success = await _partnerService.DeleteAsync(id, cancellationToken);
        if (!success) return NotFound();
        return Ok(new { message = "Partner deleted." });
    }

    [HttpPost("{id:guid}/generate-key")]
    public async Task<ActionResult> GenerateKey(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var (apiKey, prefix) = await _partnerService.GenerateApiKeyAsync(id, cancellationToken);
            return Ok(new { ApiKey = apiKey, Prefix = prefix, Warning = "Store this key securely — it will not be shown again." });
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpPut("{id:guid}/brands")]
    public async Task<ActionResult> SetBrands(Guid id, [FromBody] SetBrandsRequest request, CancellationToken cancellationToken)
    {
        await _partnerService.SetPartnerBrandsAsync(id, request.BrandIds, cancellationToken);
        return Ok(new { message = "Brand associations updated." });
    }
}

public record CreatePartnerRequest(string Name, string ContactEmail, string CallbackUrl, List<Guid>? BrandIds);
public record UpdatePartnerRequest(string Name, string ContactEmail, string CallbackUrl, bool IsActive);
public record SetBrandsRequest(List<Guid> BrandIds);
public record PartnerDto(Guid Id, string Name, string ContactEmail, string CallbackUrl, string ApiKeyPrefix, bool IsActive, DateTime CreatedAt, List<PartnerBrandDto> Brands);
public record PartnerBrandDto(Guid BrandId, string? BrandName);
