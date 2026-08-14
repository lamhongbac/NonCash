using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class EmailLogConfiguration : IEntityTypeConfiguration<EmailLog>
{
    public void Configure(EntityTypeBuilder<EmailLog> builder)
    {
        builder.ToTable("email_logs");

        builder.Property(e => e.ToAddress).IsRequired().HasMaxLength(255);
        builder.Property(e => e.Subject).IsRequired().HasMaxLength(500);
        builder.Property(e => e.TemplateName).HasMaxLength(100);
        builder.Property(e => e.NotificationType).HasMaxLength(100);
        builder.Property(e => e.ErrorMessage).HasMaxLength(2000);

        builder.HasIndex(e => e.NotificationType)
            .HasDatabaseName("IX_email_logs_notification_type");

        builder.HasIndex(e => e.SentAt)
            .HasDatabaseName("IX_email_logs_sent_at");

        builder.HasIndex(e => e.Success)
            .HasDatabaseName("IX_email_logs_success");
    }
}
