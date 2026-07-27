using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class IntegrationPartnerConfiguration : IEntityTypeConfiguration<IntegrationPartner>
{
    public void Configure(EntityTypeBuilder<IntegrationPartner> builder)
    {
        builder.ToTable("integration_partners");

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.ContactEmail).HasMaxLength(200).IsRequired();
        builder.Property(p => p.CallbackUrl).HasMaxLength(500).IsRequired();
        builder.Property(p => p.ApiKeyPrefix).HasMaxLength(16).IsRequired();
        builder.Property(p => p.ApiKeyHash).HasMaxLength(200).IsRequired();
        builder.Property(p => p.WebhookSecret).HasMaxLength(200).IsRequired();
        builder.Property(p => p.IsActive).IsRequired();

        builder.HasIndex(p => p.ApiKeyPrefix).IsUnique().HasDatabaseName("IX_integration_partners_api_key_prefix");
        builder.HasIndex(p => p.Name).HasDatabaseName("IX_integration_partners_name");
    }
}

public class PartnerBrandConfiguration : IEntityTypeConfiguration<PartnerBrand>
{
    public void Configure(EntityTypeBuilder<PartnerBrand> builder)
    {
        builder.ToTable("partner_brands");

        builder.HasKey(pb => new { pb.PartnerId, pb.BrandId });

        builder.HasOne(pb => pb.Partner)
            .WithMany(p => p.PartnerBrands)
            .HasForeignKey(pb => pb.PartnerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pb => pb.Brand)
            .WithMany()
            .HasForeignKey(pb => pb.BrandId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(pb => pb.BrandId).HasDatabaseName("IX_partner_brands_brand_id");
    }
}
