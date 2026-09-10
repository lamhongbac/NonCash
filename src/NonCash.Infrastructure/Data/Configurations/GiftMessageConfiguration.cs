using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class GiftMessageConfiguration : IEntityTypeConfiguration<GiftMessage>
{
    public void Configure(EntityTypeBuilder<GiftMessage> builder)
    {
        builder.ToTable("gift_messages");

        builder.Property(m => m.Direction)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(m => m.Kind)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(m => m.Body).IsRequired().HasMaxLength(GiftMessagePolicy.MaxBodyLength);
        builder.Property(m => m.SentAt).IsRequired();
        builder.Property(m => m.IsRead).IsRequired();
        builder.Property(m => m.ReadAt).IsRequired(false);

        builder.HasIndex(m => new { m.TransferId, m.SentAt })
            .HasDatabaseName("IX_gift_messages_transfer_id_sent_at");

        builder.HasIndex(m => new { m.RecipientMemberId, m.IsRead })
            .HasDatabaseName("IX_gift_messages_recipient_member_id_is_read");

        builder.HasOne(m => m.Transfer)
            .WithMany()
            .HasForeignKey(m => m.TransferId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Author)
            .WithMany()
            .HasForeignKey(m => m.AuthorMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Recipient)
            .WithMany()
            .HasForeignKey(m => m.RecipientMemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
