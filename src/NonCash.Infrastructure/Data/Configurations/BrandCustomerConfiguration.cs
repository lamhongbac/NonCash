using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class BrandCustomerConfiguration : IEntityTypeConfiguration<BrandCustomer>
{
    public void Configure(EntityTypeBuilder<BrandCustomer> builder)
    {
        builder.HasKey(bc => bc.Id);

        builder.ToTable("brand_customers");

        builder.Property(bc => bc.BrandId)
            .IsRequired();

        builder.HasIndex(bc => bc.BrandId);

        builder.HasOne(bc => bc.Brand)
            .WithMany()
            .HasForeignKey(bc => bc.BrandId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(bc => bc.CustomerId)
            .IsRequired();

        builder.HasIndex(bc => bc.CustomerId);

        builder.HasOne(bc => bc.Customer)
            .WithMany()
            .HasForeignKey(bc => bc.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        // One row per (brand, customer) pair — EnsureAsync relies on this.
        builder.HasIndex(bc => new { bc.BrandId, bc.CustomerId })
            .IsUnique();

        builder.Property(bc => bc.Source)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(bc => bc.CreatedBy);

        builder.Property(bc => bc.IsBlocked)
            .IsRequired();

        builder.Property(bc => bc.BlockedAt)
            .IsRequired(false);

        builder.Property(bc => bc.MarketingOptOut)
            .IsRequired();

        builder.Property(bc => bc.CreatedAt)
            .IsRequired();

        builder.Property(bc => bc.UpdatedAt)
            .IsRequired(false);
    }
}
