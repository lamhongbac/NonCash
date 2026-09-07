using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class CustomerAuditLogConfiguration : IEntityTypeConfiguration<CustomerAuditLog>
{
    public void Configure(EntityTypeBuilder<CustomerAuditLog> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CustomerId)
            .IsRequired();

        builder.HasIndex(x => x.CustomerId);

        builder.Property(x => x.Field)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.OldValue)
            .HasMaxLength(255);

        builder.Property(x => x.NewValue)
            .HasMaxLength(255);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired(false);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
