using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.API.DTOs;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.API.Controllers;

/// <summary>
/// Admin management of credit pricing policies and brand groups (Epic 10).
/// </summary>
[ApiController]
[Route("api/v1/credit-policies")]
[Authorize(Roles = "Admin")]
public class CreditPoliciesController : ControllerBase
{
    private readonly ICreditPolicyService _policyService;
    private readonly ICurrentUserService _currentUser;

    public CreditPoliciesController(ICreditPolicyService policyService, ICurrentUserService currentUser)
    {
        _policyService = policyService;
        _currentUser = currentUser;
    }

    // ----- Policies -----

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CreditPolicyDto>>> GetPolicies(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var policies = await _policyService.GetPoliciesAsync(includeInactive, cancellationToken);
        return Ok(policies.Select(ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CreditPolicyDto>> GetPolicy(Guid id, CancellationToken cancellationToken)
    {
        var policy = await _policyService.GetPolicyAsync(id, cancellationToken);
        if (policy == null)
            return NotFound();

        return Ok(ToDto(policy));
    }

    [HttpPost]
    public async Task<ActionResult<CreditPolicyDto>> CreatePolicy(
        [FromBody] SaveCreditPolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryMapPolicy(request, out var policy, out var error))
            return BadRequest(new { error });

        policy.CreatedBy = Guid.TryParse(_currentUser.GetCurrentUserId(), out var uid) ? uid : null;

        try
        {
            var created = await _policyService.CreatePolicyAsync(policy, cancellationToken);
            return Ok(ToDto(created));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CreditPolicyDto>> UpdatePolicy(
        Guid id,
        [FromBody] SaveCreditPolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryMapPolicy(request, out var changes, out var error))
            return BadRequest(new { error });

        try
        {
            var updated = await _policyService.UpdatePolicyAsync(id, changes, cancellationToken);
            return Ok(ToDto(updated));
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

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivatePolicy(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _policyService.DeactivatePolicyAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // ----- Brand groups -----

    [HttpGet("groups")]
    public async Task<ActionResult<IReadOnlyList<BrandGroupDto>>> GetGroups(CancellationToken cancellationToken)
    {
        var groups = await _policyService.GetGroupsAsync(cancellationToken);
        return Ok(groups.Select(ToDto).ToList());
    }

    [HttpGet("groups/{id:guid}")]
    public async Task<ActionResult<BrandGroupDto>> GetGroup(Guid id, CancellationToken cancellationToken)
    {
        var group = await _policyService.GetGroupAsync(id, cancellationToken);
        if (group == null)
            return NotFound();

        return Ok(ToDto(group));
    }

    [HttpPost("groups")]
    public async Task<ActionResult<BrandGroupDto>> CreateGroup(
        [FromBody] SaveBrandGroupRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var group = await _policyService.CreateGroupAsync(request.Name, request.Description, cancellationToken);
            return Ok(ToDto(group));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("groups/{id:guid}")]
    public async Task<ActionResult<BrandGroupDto>> UpdateGroup(
        Guid id,
        [FromBody] SaveBrandGroupRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var group = await _policyService.UpdateGroupAsync(id, request.Name, request.Description, request.IsActive, cancellationToken);
            return Ok(ToDto(group));
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

    [HttpPut("groups/{id:guid}/members")]
    public async Task<IActionResult> SetGroupMembers(
        Guid id,
        [FromBody] SetGroupMembersRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _policyService.SetGroupMembersAsync(id, request.BrandIds, cancellationToken);
            return NoContent();
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

    // ----- Mapping -----

    private static bool TryMapPolicy(SaveCreditPolicyRequest request, out CreditPricingPolicy policy, out string? error)
    {
        policy = null!;
        error = null;

        if (!Enum.TryParse<PolicyScope>(request.Scope, true, out var scope))
        {
            error = "Scope must be Global, BrandGroup, or Brand.";
            return false;
        }

        policy = new CreditPricingPolicy
        {
            Name = request.Name,
            Scope = scope,
            BrandGroupId = request.BrandGroupId,
            BrandId = request.BrandId,
            PricePerCreditVnd = request.PricePerCreditVnd,
            CreditExpiryMonths = request.CreditExpiryMonths,
            WelcomeCredits = request.WelcomeCredits,
            WelcomeCreditExpiryMonths = request.WelcomeCreditExpiryMonths,
            LowBalanceWarningPct = request.LowBalanceWarningPct,
            ExpiryWarningDays = request.ExpiryWarningDays,
            AdjustmentApprovalThreshold = request.AdjustmentApprovalThreshold,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            IsActive = request.IsActive
        };
        return true;
    }

    private static CreditPolicyDto ToDto(CreditPricingPolicy p) => new(
        p.Id,
        p.Name,
        p.Scope.ToString(),
        p.BrandGroupId,
        p.BrandGroup?.Name,
        p.BrandId,
        p.Brand?.Name,
        p.PricePerCreditVnd,
        p.CreditExpiryMonths,
        p.WelcomeCredits,
        p.WelcomeCreditExpiryMonths,
        p.LowBalanceWarningPct,
        p.ExpiryWarningDays,
        p.AdjustmentApprovalThreshold,
        p.EffectiveFrom,
        p.EffectiveTo,
        p.IsActive,
        p.CreatedAt);

    private static BrandGroupDto ToDto(BrandGroup g) => new(
        g.Id,
        g.Name,
        g.Description,
        g.IsActive,
        g.Members.Select(m => new BrandGroupMemberDto(m.BrandId, m.Brand?.Name)).ToList());
}
