using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class VoucherEventConfiguration : IEntityTypeConfiguration<VoucherEvent>
{
    public void Configure(EntityTypeBuilder<VoucherEvent> builder)
    {
        builder.ToTable("voucher_events");

        builder.Property(e => e.EventType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.MemberPhone).HasMaxLength(20);
        builder.Property(e => e.PayloadJson).HasColumnType("text").IsRequired();

        builder.HasIndex(e => e.EventType).HasDatabaseName("IX_voucher_events_event_type");
        builder.HasIndex(e => e.MemberPhone).HasDatabaseName("IX_voucher_events_member_phone");
        builder.HasIndex(e => e.BrandId).HasDatabaseName("IX_voucher_events_brand_id");
        builder.HasIndex(e => e.CreatedAt).HasDatabaseName("IX_voucher_events_created_at");

        builder.HasOne(e => e.Voucher)
            .WithMany()
            .HasForeignKey(e => e.VoucherId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Brand)
            .WithMany()
            .HasForeignKey(e => e.BrandId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("webhook_deliveries");

        builder.Property(d => d.LastError).HasMaxLength(1000);

        builder.HasIndex(d => d.PartnerId).HasDatabaseName("IX_webhook_deliveries_partner_id");
        builder.HasIndex(d => d.DeliveredAt).HasDatabaseName("IX_webhook_deliveries_delivered_at");
        builder.HasIndex(d => d.NextRetryAt).HasDatabaseName("IX_webhook_deliveries_next_retry_at");
        builder.HasIndex(d => new { d.EventId, d.PartnerId }).IsUnique()
            .HasDatabaseName("IX_webhook_deliveries_event_partner");

        builder.HasOne(d => d.Partner)
            .WithMany()
            .HasForeignKey(d => d.PartnerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Event)
            .WithMany(e => e.Deliveries)
            .HasForeignKey(d => d.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
