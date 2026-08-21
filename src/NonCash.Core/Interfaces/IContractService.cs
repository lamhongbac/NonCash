using NonCash.Core.Configuration;

namespace NonCash.Core.Interfaces;

/// <summary>
/// Generates a contract document (HTML) for a business registration request based on the
/// selected welcome policy template and the active contract template. The HTML can be printed
/// from the browser or included in the contract-sent email.
/// </summary>
public interface IContractService
{
    /// <summary>
    /// Renders the contract HTML using the active (or specified) contract template and replaces
    /// placeholders with values from <paramref name="data"/> and <paramref name="platformTerms"/>.
    /// </summary>
    Task<string> GenerateContractHtmlAsync(
        ContractData data,
        CreditConfig platformTerms,
        Guid? templateId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Values supplied by the registration request and selected welcome policy template.
/// </summary>
public record ContractData(
    string BusinessName,
    string BrandName,
    string TaxCode,
    string? RepresentativeName,
    string PolicyTemplateName,
    int WelcomeCredits,
    int? WelcomeCreditExpiryMonths);
