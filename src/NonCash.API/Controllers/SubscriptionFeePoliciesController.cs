using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.API.Controllers;

/// <summary>
/// Admin management of date-ranged subscription fee policies.
/// </summary>
[ApiController]
[Route("api/v1/subscription-fee-policies")]
[Authorize(Roles = "Admin")]
public class SubscriptionFeePoliciesController : ControllerBase
{
    private readonly ISubscriptionFeePolicyService _policyService;
    private readonly ICurrentUserService _currentUser;

    public SubscriptionFeePoliciesController(ISubscriptionFeePolicyService policyService, ICurrentUserService currentUser)
    {
        _policyService = policyService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SubscriptionFeePolicyDto>>> GetPolicies(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var policies = await _policyService.GetPoliciesAsync(includeInactive, cancellationToken);
        return Ok(policies.Select(ToDto).ToList());
    }

    [HttpGet("effective")]
    public async Task<ActionResult<SubscriptionFeePolicyDto>> GetEffectivePolicy(CancellationToken cancellationToken)
    {
        var policy = await _policyService.GetEffectivePolicyAsync(cancellationToken: cancellationToken);
        if (policy is null)
            return NotFound(new { error = "No active subscription fee policy found for the current date." });

        return Ok(ToDto(policy));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SubscriptionFeePolicyDto>> GetPolicy(Guid id, CancellationToken cancellationToken)
    {
        var policy = await _policyService.GetPolicyAsync(id, cancellationToken);
        if (policy is null)
            return NotFound(new { error = "Subscription fee policy not found." });

        return Ok(ToDto(policy));
    }

    [HttpPost]
    public async Task<ActionResult<SubscriptionFeePolicyDto>> CreatePolicy(
        [FromBody] SaveSubscriptionFeePolicyDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var policy = MapFromDto(dto);
            var created = await _policyService.CreatePolicyAsync(policy, ParseUserId(_currentUser.GetCurrentUserId()), cancellationToken);
            return CreatedAtAction(nameof(GetPolicy), new { id = created.Id }, ToDto(created));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SubscriptionFeePolicyDto>> UpdatePolicy(
        Guid id,
        [FromBody] SaveSubscriptionFeePolicyDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var policy = MapFromDto(dto);
            var updated = await _policyService.UpdatePolicyAsync(id, policy, ParseUserId(_currentUser.GetCurrentUserId()), cancellationToken);
            if (updated is null)
                return NotFound(new { error = "Subscription fee policy not found." });

            return Ok(ToDto(updated));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static SubscriptionFeePolicy MapFromDto(SaveSubscriptionFeePolicyDto dto)
        => new()
        {
            Name = dto.Name.Trim(),
            AmountVnd = dto.AmountVnd,
            IsFree = dto.IsFree,
            MinimumCommitmentMonths = dto.MinimumCommitmentMonths,
            EffectiveFrom = EnsureUtc(dto.EffectiveFrom),
            EffectiveTo = dto.EffectiveTo.HasValue ? EnsureUtc(dto.EffectiveTo.Value) : null,
            IsActive = dto.IsActive
        };

    private static DateTime EnsureUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc) return value;
        if (value.Kind == DateTimeKind.Local) return value.ToUniversalTime();
        return DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    private static SubscriptionFeePolicyDto ToDto(SubscriptionFeePolicy p)
        => new(
            p.Id,
            p.Name,
            p.AmountVnd,
            p.IsFree,
            p.MinimumCommitmentMonths,
            p.EffectiveFrom,
            p.EffectiveTo,
            p.IsActive,
            p.CreatedAt,
            p.UpdatedAt ?? p.CreatedAt);

    private static Guid? ParseUserId(string? userId)
        => !string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var id) ? id : null;
}

public record SubscriptionFeePolicyDto(
    Guid Id,
    string Name,
    decimal AmountVnd,
    bool IsFree,
    int MinimumCommitmentMonths,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public class SaveSubscriptionFeePolicyDto
{
    public string Name { get; set; } = string.Empty;
    public decimal AmountVnd { get; set; }
    public bool IsFree { get; set; }
    public int MinimumCommitmentMonths { get; set; } = 12;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}
