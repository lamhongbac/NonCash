namespace NonCash.Core.Entities;

/// <summary>
/// Editable HTML template for the business registration contract.
/// Exactly one active template may be marked as the default; it is used when generating
/// the contract for a pending registration request.
/// </summary>
public class ContractTemplate : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Full HTML contract body. Placeholders such as {{BusinessName}}, {{SubscriptionFeeVnd}},
    /// {{MinimumCommitmentMonths}} are replaced at render time.
    /// </summary>
    public string HtmlTemplate { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// True when this template should be used as the fallback for contract generation.
    /// </summary>
    public bool IsDefault { get; set; }

    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}
