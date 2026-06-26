using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("payment_transactions");

        builder.Property(t => t.PurchaseOrderId).IsRequired();
        builder.Property(t => t.Gateway).HasMaxLength(50).IsRequired();
        builder.Property(t => t.GatewayTransactionId).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Amount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(t => t.Currency).HasMaxLength(10).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(t => t.RequestPayload).HasColumnType("text");
        builder.Property(t => t.ResponsePayload).HasColumnType("text");
        builder.Property(t => t.WebhookPayload).HasColumnType("text");
        builder.Property(t => t.GatewayResponseCode).HasMaxLength(50);

        builder.HasIndex(t => t.PurchaseOrderId).HasDatabaseName("IX_payment_transactions_purchase_order_id");
        builder.HasIndex(t => t.GatewayTransactionId).HasDatabaseName("IX_payment_transactions_gateway_transaction_id");
        builder.HasIndex(t => t.Status).HasDatabaseName("IX_payment_transactions_status");

        builder.HasOne(t => t.PurchaseOrder)
            .WithMany()
            .HasForeignKey(t => t.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
