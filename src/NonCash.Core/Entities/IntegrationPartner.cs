namespace NonCash.Core.Entities;

/// <summary>
/// Represents an external loyalty app or CRM system that integrates with NonCash.
/// Partners use API keys to access the Integration API for distributing vouchers,
/// querying member wallets, and receiving webhook events.
/// </summary>
public class IntegrationPartner : BaseEntity
{
    /// <summary>Display name of the integration partner (e.g. "Giga Mall App").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Technical contact email for the partner.</summary>
    public string ContactEmail { get; set; } = string.Empty;

    /// <summary>URL where NonCash will deliver webhook events.</summary>
    public string CallbackUrl { get; set; } = string.Empty;

    /// <summary>First 8 chars of the API key (for identification in logs/UI).</summary>
    public string ApiKeyPrefix { get; set; } = string.Empty;

    /// <summary>BCrypt hash of the full API key.</summary>
    public string ApiKeyHash { get; set; } = string.Empty;

    /// <summary>Shared secret for HMAC-SHA256 webhook signatures.</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>Whether this partner is currently active and can make API calls.</summary>
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<PartnerBrand> PartnerBrands { get; set; } = new List<PartnerBrand>();
}

/// <summary>
/// Join entity linking an IntegrationPartner to the Brands they are authorized to operate on.
/// </summary>
public class PartnerBrand
{
    public Guid PartnerId { get; set; }
    public Guid BrandId { get; set; }

    public IntegrationPartner? Partner { get; set; }
    public Brand? Brand { get; set; }
}
