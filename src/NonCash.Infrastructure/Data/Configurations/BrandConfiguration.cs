using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.BusinessId)
            .IsRequired();

        builder.HasIndex(b => b.BusinessId);

        builder.HasOne(b => b.Business)
            .WithMany(bu => bu.Brands)
            .HasForeignKey(b => b.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.TaxCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(b => b.TaxCode)
            .IsUnique();

        builder.Property(b => b.ContactEmail)
            .HasMaxLength(255);

        builder.Property(b => b.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(b => b.Status);

        builder.Property(b => b.CreatedAt)
            .IsRequired();

        builder.Property(b => b.UpdatedAt)
            .IsRequired(false);
    }
}
