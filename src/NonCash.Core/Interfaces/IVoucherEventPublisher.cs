using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

/// <summary>
/// Publishes voucher lifecycle events to the outbox (VoucherEvent table).
/// Called from business services (PosService, PromotionService, TransferService)
/// within the same DB transaction as the business operation.
/// The WebhookDeliveryService (BackgroundService) later reads and delivers these events.
/// </summary>
public interface IVoucherEventPublisher
{
    /// <summary>
    /// Writes a VoucherEvent to the outbox and creates pending WebhookDelivery records for all active partners
    /// that are associated with the given brand.
    /// </summary>
    Task PublishAsync(
        string eventType,
        Guid? voucherId,
        string? memberPhone,
        Guid? brandId,
        object payload,
        CancellationToken cancellationToken = default);
}
