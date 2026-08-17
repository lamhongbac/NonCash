using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

/// <summary>
/// Welcome-grant policy template management, assignment, and resolution.
/// Templates are reusable across businesses; assignments are per-business instances.
/// Unassigned businesses fall back to the single default template.
/// </summary>
public interface IWelcomePolicyService
{
    /// <summary>
    /// Resolves the effective welcome policy for a business
    /// (Business assignment → default template → <c>CreditConfig</c> fallback).
    /// </summary>
    Task<ResolvedWelcomePolicy> ResolveForBusinessAsync(Guid businessId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a template to a business, creating or replacing the active business assignment.
    /// When <paramref name="templateId"/> is null, the current default template is used.
    /// </summary>
    Task<WelcomeGrantPolicy> AssignTemplateToBusinessAsync(Guid businessId, Guid? templateId, Guid? actingUserId = null, CancellationToken cancellationToken = default);

    // ----- Template CRUD (Admin) -----
    Task<IReadOnlyList<WelcomeGrantPolicyTemplate>> GetTemplatesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<WelcomeGrantPolicyTemplate?> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WelcomeGrantPolicyTemplate?> GetDefaultTemplateAsync(CancellationToken cancellationToken = default);
    Task<WelcomeGrantPolicyTemplate> CreateTemplateAsync(WelcomeGrantPolicyTemplate template, CancellationToken cancellationToken = default);
    Task<WelcomeGrantPolicyTemplate> UpdateTemplateAsync(Guid id, WelcomeGrantPolicyTemplate changes, CancellationToken cancellationToken = default);
    Task DeactivateTemplateAsync(Guid id, CancellationToken cancellationToken = default);
    Task SetDefaultTemplateAsync(Guid id, CancellationToken cancellationToken = default);

    // ----- Assignment CRUD (Admin) -----
    Task<IReadOnlyList<WelcomeGrantPolicy>> GetPoliciesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<WelcomeGrantPolicy?> GetPolicyAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WelcomeGrantPolicy> CreatePolicyAsync(WelcomeGrantPolicy policy, CancellationToken cancellationToken = default);
    Task<WelcomeGrantPolicy> UpdatePolicyAsync(Guid id, WelcomeGrantPolicy changes, CancellationToken cancellationToken = default);
    Task DeactivatePolicyAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// The welcome policy values in force for a business, after Business assignment →
/// default template → <c>CreditConfig</c> fallback resolution.
/// PolicyId is null when no DB assignment matched (fallback).
/// </summary>
public record ResolvedWelcomePolicy(
    Guid? PolicyId,
    string Name,
    int WelcomeCredits,
    int? WelcomeCreditExpiryMonths);
