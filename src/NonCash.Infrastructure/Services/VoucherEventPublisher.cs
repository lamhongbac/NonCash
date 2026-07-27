using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Services;

/// <summary>
/// Writes VoucherEvent + WebhookDelivery records to the outbox.
/// The WebhookDeliveryService picks these up asynchronously.
/// </summary>
public class VoucherEventPublisher : IVoucherEventPublisher
{
    private readonly ApplicationDbContext _context;

    public VoucherEventPublisher(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task PublishAsync(
        string eventType,
        Guid? voucherId,
        string? memberPhone,
        Guid? brandId,
        object payload,
        CancellationToken cancellationToken = default)
    {
        var payloadJson = JsonSerializer.Serialize(payload);

        var evt = new VoucherEvent
        {
            EventType = eventType,
            VoucherId = voucherId,
            MemberPhone = memberPhone,
            BrandId = brandId,
            PayloadJson = payloadJson
        };

        _context.Set<VoucherEvent>().Add(evt);
        await _context.SaveChangesAsync(cancellationToken);

        // Create pending deliveries for all active partners associated with this brand
        if (brandId.HasValue)
        {
            var partnerIds = await _context.Set<PartnerBrand>()
                .Where(pb => pb.BrandId == brandId.Value)
                .Select(pb => pb.PartnerId)
                .ToListAsync(cancellationToken);

            // Filter to only active partners
            var activePartnerIds = await _context.Set<IntegrationPartner>()
                .Where(p => partnerIds.Contains(p.Id) && p.IsActive)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            foreach (var partnerId in activePartnerIds)
            {
                _context.Set<WebhookDelivery>().Add(new WebhookDelivery
                {
                    PartnerId = partnerId,
                    EventId = evt.Id,
                    RetryCount = 0,
                    NextRetryAt = DateTime.UtcNow // Eligible for immediate delivery
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
