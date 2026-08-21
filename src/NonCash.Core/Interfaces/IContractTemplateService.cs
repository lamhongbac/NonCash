using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

/// <summary>
/// Management of editable contract templates used for business registration agreements.
/// </summary>
public interface IContractTemplateService
{
    Task<IReadOnlyList<ContractTemplate>> GetTemplatesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<ContractTemplate?> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ContractTemplate?> GetDefaultTemplateAsync(CancellationToken cancellationToken = default);
    Task<ContractTemplate> CreateTemplateAsync(string name, string htmlTemplate, bool isDefault, Guid? actingUserId = null, CancellationToken cancellationToken = default);
    Task<ContractTemplate?> UpdateTemplateAsync(Guid id, string name, string htmlTemplate, bool isActive, bool isDefault, Guid? actingUserId = null, CancellationToken cancellationToken = default);
    Task<bool> SetDefaultTemplateAsync(Guid id, CancellationToken cancellationToken = default);
}
