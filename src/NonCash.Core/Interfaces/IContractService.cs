namespace NonCash.Core.Interfaces;

/// <summary>
/// Generates a contract document (HTML) for a business registration request based on the
/// selected welcome policy template. The HTML can be printed from the browser or included
/// in the contract-sent email.
/// </summary>
public interface IContractService
{
    Task<string> GenerateContractHtmlAsync(
        string businessName,
        string brandName,
        string taxCode,
        string? representativeName,
        string policyTemplateName,
        int welcomeCredits,
        int? welcomeCreditExpiryMonths,
        CancellationToken cancellationToken = default);
}
