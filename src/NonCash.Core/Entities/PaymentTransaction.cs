namespace NonCash.Core.Entities;

public enum PaymentStatus
{
    Pending,
    Success,
    Failed,
    Cancelled,
    Refunded
}

public class PaymentTransaction : BaseEntity
{
    public Guid PurchaseOrderId { get; set; }
    public string Gateway { get; set; } = string.Empty;
    public string GatewayTransactionId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public string? RequestPayload { get; set; }
    public string? ResponsePayload { get; set; }
    public string? WebhookPayload { get; set; }
    public string? GatewayResponseCode { get; set; }

    public DateTime? CompletedAt { get; set; }

    public PurchaseOrder? PurchaseOrder { get; set; }
}
