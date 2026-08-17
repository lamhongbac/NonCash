using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.API.DTOs;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.API.Controllers;

/// <summary>
/// Admin management of reusable welcome-grant policy templates.
/// Exactly one template may be marked as the default; unassigned businesses use it on approval.
/// </summary>
[ApiController]
[Route("api/v1/welcome-policy-templates")]
[Authorize(Roles = "Admin")]
public class WelcomePolicyTemplatesController : ControllerBase
{
    private readonly IWelcomePolicyService _welcomeService;
    private readonly ICurrentUserService _currentUser;

    public WelcomePolicyTemplatesController(IWelcomePolicyService welcomeService, ICurrentUserService currentUser)
    {
        _welcomeService = welcomeService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WelcomePolicyTemplateDto>>> GetTemplates(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var templates = await _welcomeService.GetTemplatesAsync(includeInactive, cancellationToken);
        return Ok(templates.Select(ToDto).ToList());
    }

    [HttpGet("default")]
    public async Task<ActionResult<WelcomePolicyTemplateDto>> GetDefaultTemplate(CancellationToken cancellationToken)
    {
        var template = await _welcomeService.GetDefaultTemplateAsync(cancellationToken);
        return template == null ? NotFound() : Ok(ToDto(template));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WelcomePolicyTemplateDto>> GetTemplate(Guid id, CancellationToken cancellationToken)
    {
        var template = await _welcomeService.GetTemplateAsync(id, cancellationToken);
        return template == null ? NotFound() : Ok(ToDto(template));
    }

    [HttpPost]
    public async Task<ActionResult<WelcomePolicyTemplateDto>> CreateTemplate(
        [FromBody] SaveWelcomePolicyTemplateRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryMapTemplate(request, out var template, out var error))
            return BadRequest(new { error });

        template.CreatedBy = Guid.TryParse(_currentUser.GetCurrentUserId(), out var uid) ? uid : null;

        try
        {
            var created = await _welcomeService.CreateTemplateAsync(template, cancellationToken);
            return Ok(ToDto(created));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WelcomePolicyTemplateDto>> UpdateTemplate(
        Guid id,
        [FromBody] SaveWelcomePolicyTemplateRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryMapTemplate(request, out var changes, out var error))
            return BadRequest(new { error });

        changes.UpdatedBy = Guid.TryParse(_currentUser.GetCurrentUserId(), out var uid) ? uid : null;

        try
        {
            var updated = await _welcomeService.UpdateTemplateAsync(id, changes, cancellationToken);
            return Ok(ToDto(updated));
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { error = ex.Message })
                : BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateTemplate(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _welcomeService.DeactivateTemplateAsync(id, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/set-default")]
    public async Task<IActionResult> SetDefaultTemplate(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _welcomeService.SetDefaultTemplateAsync(id, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ----- Mapping -----

    private static bool TryMapTemplate(SaveWelcomePolicyTemplateRequest request, out WelcomeGrantPolicyTemplate template, out string? error)
    {
        template = null!;
        error = null;

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            error = "Name is required.";
            return false;
        }

        template = new WelcomeGrantPolicyTemplate
        {
            Name = request.Name.Trim(),
            WelcomeCredits = request.WelcomeCredits,
            WelcomeCreditExpiryMonths = request.WelcomeCreditExpiryMonths,
            IsActive = request.IsActive,
            IsDefault = request.IsDefault
        };
        return true;
    }

    private static WelcomePolicyTemplateDto ToDto(WelcomeGrantPolicyTemplate t) => new(
        t.Id,
        t.Name,
        t.WelcomeCredits,
        t.WelcomeCreditExpiryMonths,
        t.IsActive,
        t.IsDefault,
        t.CreatedAt);
}
