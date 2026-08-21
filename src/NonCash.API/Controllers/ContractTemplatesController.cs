using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.API.Controllers;

/// <summary>
/// Admin management of editable contract templates used for business registration agreements.
/// </summary>
[ApiController]
[Route("api/v1/contract-templates")]
[Authorize(Roles = "Admin")]
public class ContractTemplatesController : ControllerBase
{
    private readonly IContractTemplateService _templateService;
    private readonly ICurrentUserService _currentUser;

    public ContractTemplatesController(IContractTemplateService templateService, ICurrentUserService currentUser)
    {
        _templateService = templateService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContractTemplateDto>>> GetTemplates(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var templates = await _templateService.GetTemplatesAsync(includeInactive, cancellationToken);
        return Ok(templates.Select(ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContractTemplateDto>> GetTemplate(Guid id, CancellationToken cancellationToken)
    {
        var template = await _templateService.GetTemplateAsync(id, cancellationToken);
        if (template is null)
            return NotFound(new { error = "Contract template not found." });

        return Ok(ToDto(template));
    }

    [HttpGet("default")]
    public async Task<ActionResult<ContractTemplateDto>> GetDefaultTemplate(CancellationToken cancellationToken)
    {
        var template = await _templateService.GetDefaultTemplateAsync(cancellationToken);
        if (template is null)
            return NotFound(new { error = "No default contract template found." });

        return Ok(ToDto(template));
    }

    [HttpPost]
    public async Task<ActionResult<ContractTemplateDto>> CreateTemplate(
        [FromBody] SaveContractTemplateDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var template = await _templateService.CreateTemplateAsync(
                dto.Name,
                dto.HtmlTemplate,
                dto.IsDefault,
                ParseUserId(_currentUser.GetCurrentUserId()),
                cancellationToken);

            return CreatedAtAction(nameof(GetTemplate), new { id = template.Id }, ToDto(template));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ContractTemplateDto>> UpdateTemplate(
        Guid id,
        [FromBody] SaveContractTemplateDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var template = await _templateService.UpdateTemplateAsync(
                id,
                dto.Name,
                dto.HtmlTemplate,
                dto.IsActive,
                dto.IsDefault,
                ParseUserId(_currentUser.GetCurrentUserId()),
                cancellationToken);

            if (template is null)
                return NotFound(new { error = "Contract template not found." });

            return Ok(ToDto(template));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/set-default")]
    public async Task<ActionResult> SetDefaultTemplate(Guid id, CancellationToken cancellationToken)
    {
        var success = await _templateService.SetDefaultTemplateAsync(id, cancellationToken);
        if (!success)
            return NotFound(new { error = "Contract template not found or inactive." });

        return Ok(new { message = "Default template updated." });
    }

    private static ContractTemplateDto ToDto(ContractTemplate t)
        => new(
            t.Id,
            t.Name,
            t.HtmlTemplate,
            t.IsActive,
            t.IsDefault,
            t.CreatedAt,
            t.UpdatedAt ?? t.CreatedAt);

    private static Guid? ParseUserId(string? userId)
        => !string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var id) ? id : null;
}

public record ContractTemplateDto(
    Guid Id,
    string Name,
    string HtmlTemplate,
    bool IsActive,
    bool IsDefault,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public class SaveContractTemplateDto
{
    public string Name { get; set; } = string.Empty;
    public string HtmlTemplate { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
}
