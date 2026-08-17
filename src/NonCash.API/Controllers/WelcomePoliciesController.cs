using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NonCash.API.DTOs;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.API.Controllers;

/// <summary>
/// Admin management of welcome-grant policies (Epic 10 refactor).
/// Welcome is a per-business commercial term: every new brand a business launches
/// receives the policy's <see cref="WelcomeGrantPolicy.WelcomeCredits"/> on activation.
/// </summary>
[ApiController]
[Route("api/v1/welcome-policies")]
[Authorize(Roles = "Admin")]
public class WelcomePoliciesController : ControllerBase
{
    private readonly IWelcomePolicyService _welcomeService;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public WelcomePoliciesController(IWelcomePolicyService welcomeService, ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _welcomeService = welcomeService;
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>Businesses available as a welcome-policy target (for the admin dropdown).</summary>
    [HttpGet("businesses")]
    public async Task<ActionResult<IReadOnlyList<BusinessLookupDto>>> GetBusinesses(CancellationToken cancellationToken)
    {
        var businesses = await _db.Businesses
            .AsNoTracking()
            .OrderBy(b => b.BusinessName)
            .Select(b => new BusinessLookupDto(b.Id, b.BusinessName))
            .ToListAsync(cancellationToken);

        return Ok(businesses);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WelcomePolicyDto>>> GetPolicies(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var policies = await _welcomeService.GetPoliciesAsync(includeInactive, cancellationToken);
        return Ok(policies.Select(ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WelcomePolicyDto>> GetPolicy(Guid id, CancellationToken cancellationToken)
    {
        var policy = await _welcomeService.GetPolicyAsync(id, cancellationToken);
        if (policy == null)
            return NotFound();

        return Ok(ToDto(policy));
    }

    /// <summary>Resolves the effective welcome policy for a business (Business policy → config fallback).</summary>
    [HttpGet("resolve")]
    public async Task<ActionResult<ResolvedWelcomePolicyResponse>> Resolve(
        [FromQuery] Guid businessId,
        CancellationToken cancellationToken)
    {
        if (businessId == Guid.Empty)
            return BadRequest(new { error = "businessId is required." });

        var resolved = await _welcomeService.ResolveForBusinessAsync(businessId, cancellationToken);
        return Ok(new ResolvedWelcomePolicyResponse(
            resolved.PolicyId,
            resolved.Name,
            resolved.WelcomeCredits,
            resolved.WelcomeCreditExpiryMonths));
    }

    [HttpPost]
    public async Task<ActionResult<WelcomePolicyDto>> CreatePolicy(
        [FromBody] SaveWelcomePolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryMapPolicy(request, out var policy, out var error))
            return BadRequest(new { error });

        policy.CreatedBy = Guid.TryParse(_currentUser.GetCurrentUserId(), out var uid) ? uid : null;

        try
        {
            var created = await _welcomeService.CreatePolicyAsync(policy, cancellationToken);
            return Ok(ToDto(created));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WelcomePolicyDto>> UpdatePolicy(
        Guid id,
        [FromBody] SaveWelcomePolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryMapPolicy(request, out var changes, out var error))
            return BadRequest(new { error });

        try
        {
            var updated = await _welcomeService.UpdatePolicyAsync(id, changes, cancellationToken);
            return Ok(ToDto(updated));
        }
        catch (InvalidOperationException ex)
        {
            // Not-found and validation both surface as InvalidOperationException here.
            return ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { error = ex.Message })
                : BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivatePolicy(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _welcomeService.DeactivatePolicyAsync(id, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ----- Mapping -----

    private static bool TryMapPolicy(SaveWelcomePolicyRequest request, out WelcomeGrantPolicy policy, out string? error)
    {
        policy = null!;
        error = null;

        if (request.BusinessId == Guid.Empty)
        {
            error = "BusinessId is required.";
            return false;
        }

        policy = new WelcomeGrantPolicy
        {
            Name = request.Name,
            BusinessId = request.BusinessId,
            WelcomeGrantPolicyTemplateId = request.WelcomeGrantPolicyTemplateId,
            WelcomeCredits = request.WelcomeCredits,
            WelcomeCreditExpiryMonths = request.WelcomeCreditExpiryMonths,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            IsActive = request.IsActive
        };
        return true;
    }

    private static WelcomePolicyDto ToDto(WelcomeGrantPolicy p) => new(
        p.Id,
        p.Name,
        p.BusinessId,
        p.Business?.BusinessName,
        p.WelcomeGrantPolicyTemplateId,
        p.WelcomeGrantPolicyTemplate?.Name,
        p.WelcomeCredits,
        p.WelcomeCreditExpiryMonths,
        p.EffectiveFrom,
        p.EffectiveTo,
        p.IsActive,
        p.CreatedAt);
}
