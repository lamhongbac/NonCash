using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.BrandId);
        builder.HasIndex(u => u.BrandId);
        builder.HasOne(u => u.Brand)
            .WithMany()
            .HasForeignKey(u => u.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(u => u.Username)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(u => u.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.Email)
            .HasMaxLength(255);

        builder.Property(u => u.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(u => u.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(u => u.PasswordResetToken)
            .HasMaxLength(255);

        builder.HasIndex(u => u.PasswordResetToken)
            .HasDatabaseName("IX_user_accounts_password_reset_token")
            .HasFilter("password_reset_token IS NOT NULL");

        builder.Property(u => u.PasswordResetTokenExpiry);

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        builder.Property(u => u.UpdatedAt)
            .IsRequired(false);
    }
}
