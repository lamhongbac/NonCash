using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class MemberAccountConfiguration : IEntityTypeConfiguration<MemberAccount>
{
    public void Configure(EntityTypeBuilder<MemberAccount> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.CustomerId).IsRequired();
        builder.HasIndex(u => u.CustomerId).IsUnique().HasDatabaseName("IX_member_accounts_customer_id");
        builder.HasOne(u => u.Customer)
            .WithMany()
            .HasForeignKey(u => u.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(u => u.Username)
            .IsUnique()
            .HasDatabaseName("IX_member_accounts_username");

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(u => u.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        builder.Property(u => u.UpdatedAt)
            .IsRequired(false);
    }
}
