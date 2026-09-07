using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class OutletConfiguration : IEntityTypeConfiguration<Outlet>
{
    public void Configure(EntityTypeBuilder<Outlet> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.BrandId)
            .IsRequired();

        builder.HasIndex(o => o.BrandId);

        builder.HasOne(o => o.Brand)
            .WithMany()
            .HasForeignKey(o => o.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(o => o.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(o => o.Address);

        builder.Property(o => o.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(o => o.Status);

        builder.Property(o => o.ApiKeyPrefix)
            .HasMaxLength(16);

        builder.HasIndex(o => o.ApiKeyPrefix);

        // CR-2026-09-07-18: short store code for POS app login. Nullable initially to allow
        // backfill; unique per brand so two outlets in the same brand cannot share a code.
        builder.Property(o => o.Code)
            .HasMaxLength(20);

        builder.HasIndex(o => new { o.BrandId, o.Code })
            .IsUnique()
            .HasFilter("code IS NOT NULL");

        builder.Property(o => o.CreatedAt)
            .IsRequired();

        builder.Property(o => o.UpdatedAt)
            .IsRequired(false);
    }
}
