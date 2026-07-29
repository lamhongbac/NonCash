using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class CreditPricingPolicyConfiguration : IEntityTypeConfiguration<CreditPricingPolicy>
{
    public void Configure(EntityTypeBuilder<CreditPricingPolicy> builder)
    {
        builder.ToTable("credit_pricing_policies");

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Scope).IsRequired();
        builder.Property(p => p.PricePerCreditVnd).HasPrecision(18, 2);
        builder.Property(p => p.EffectiveFrom).IsRequired();

        builder.HasIndex(p => new { p.Scope, p.IsActive, p.EffectiveFrom })
            .HasDatabaseName("IX_credit_pricing_policies_scope_active_from");

        builder.HasIndex(p => p.BrandId)
            .HasFilter("brand_id IS NOT NULL")
            .HasDatabaseName("IX_credit_pricing_policies_brand_id");

        builder.HasIndex(p => p.BrandGroupId)
            .HasFilter("brand_group_id IS NOT NULL")
            .HasDatabaseName("IX_credit_pricing_policies_brand_group_id");

        builder.HasOne(p => p.BrandGroup)
            .WithMany()
            .HasForeignKey(p => p.BrandGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Brand)
            .WithMany()
            .HasForeignKey(p => p.BrandId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
