using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class VoucherTransferConfiguration : IEntityTypeConfiguration<VoucherTransfer>
{
    public void Configure(EntityTypeBuilder<VoucherTransfer> builder)
    {
        builder.ToTable("voucher_transfers");

        builder.Property(t => t.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(t => t.TransferType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.InitiatedAt).IsRequired();
        builder.Property(t => t.ExpiresAt).IsRequired();
        builder.Property(t => t.Note).HasMaxLength(500);
        builder.Property(t => t.RejectReason).HasMaxLength(500);
        builder.Property(t => t.RespondedAt).IsRequired(false);

        builder.HasIndex(t => t.VoucherId).HasDatabaseName("IX_voucher_transfers_voucher_id");
        builder.HasIndex(t => t.SenderId).HasDatabaseName("IX_voucher_transfers_sender_id");
        builder.HasIndex(t => t.RecipientId).HasDatabaseName("IX_voucher_transfers_recipient_id");
        builder.HasIndex(t => t.Status).HasDatabaseName("IX_voucher_transfers_status");
        builder.HasIndex(t => t.ExpiresAt).HasDatabaseName("IX_voucher_transfers_expires_at");

        builder.HasOne(t => t.Voucher)
            .WithMany()
            .HasForeignKey(t => t.VoucherId)
            .OnDelete(DeleteBehavior.Restrict);

        // Sender and Recipient are UserAccounts (from JWT), not Customers
        builder.HasOne(t => t.Sender)
            .WithMany()
            .HasForeignKey(t => t.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Recipient)
            .WithMany()
            .HasForeignKey(t => t.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
