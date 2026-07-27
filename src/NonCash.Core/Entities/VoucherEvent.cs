namespace NonCash.Core.Entities;

/// <summary>
/// Outbox-pattern event record. Written in the same DB transaction as the business operation
/// that triggered it. The WebhookDeliveryService reads unprocessed events and delivers them
/// to active IntegrationPartners.
/// </summary>
public class VoucherEvent : BaseEntity
{
    /// <summary>Type of event (e.g. "voucher.distributed", "voucher.redeemed", "voucher.transferred").</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>The VoucherPlanDetail (voucher) this event relates to.</summary>
    public Guid? VoucherId { get; set; }

    /// <summary>Member phone number involved in the event (for member-scoped queries).</summary>
    public string? MemberPhone { get; set; }

    /// <summary>Brand context for the event.</summary>
    public Guid? BrandId { get; set; }

    /// <summary>JSON payload with event-specific data.</summary>
    public string PayloadJson { get; set; } = "{}";

    // Navigation
    public VoucherPlanDetail? Voucher { get; set; }
    public Brand? Brand { get; set; }
    public ICollection<WebhookDelivery> Deliveries { get; set; } = new List<WebhookDelivery>();
}

/// <summary>
/// Tracks delivery of a VoucherEvent to a specific IntegrationPartner.
/// Supports retries with exponential backoff.
/// </summary>
public class WebhookDelivery : BaseEntity
{
    /// <summary>The partner this delivery targets.</summary>
    public Guid PartnerId { get; set; }

    /// <summary>The event being delivered.</summary>
    public Guid EventId { get; set; }

    /// <summary>Last HTTP status code received from the partner's callback URL.</summary>
    public int? HttpStatus { get; set; }

    /// <summary>Number of delivery attempts so far.</summary>
    public int RetryCount { get; set; }

    /// <summary>When the event was last successfully delivered (HTTP 2xx).</summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>Next scheduled retry time (null if delivered or max retries exceeded).</summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>Last error message if delivery failed.</summary>
    public string? LastError { get; set; }

    // Navigation
    public IntegrationPartner? Partner { get; set; }
    public VoucherEvent? Event { get; set; }
}
