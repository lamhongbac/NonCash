using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

/// <summary>
/// Manages integration partner CRUD, API key generation/validation, and partner-brand associations.
/// </summary>
public interface IIntegrationPartnerService
{
    Task<IntegrationPartner> CreateAsync(string name, string contactEmail, string callbackUrl, List<Guid> brandIds, CancellationToken cancellationToken = default);
    Task<IntegrationPartner?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IntegrationPartner>> ListAsync(CancellationToken cancellationToken = default);
    Task<IntegrationPartner?> UpdateAsync(Guid id, string name, string contactEmail, string callbackUrl, bool isActive, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Generates a new API key for the partner. Returns the plaintext key (shown once).</summary>
    Task<(string ApiKey, string Prefix)> GenerateApiKeyAsync(Guid partnerId, CancellationToken cancellationToken = default);

    /// <summary>Validates an API key and returns the partner + authorized brand IDs if valid.</summary>
    Task<(IntegrationPartner? Partner, List<Guid> BrandIds)> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);

    /// <summary>Updates the brand associations for a partner.</summary>
    Task SetPartnerBrandsAsync(Guid partnerId, List<Guid> brandIds, CancellationToken cancellationToken = default);
}
