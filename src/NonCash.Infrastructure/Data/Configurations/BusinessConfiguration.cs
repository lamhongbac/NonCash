using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.BusinessName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.TaxCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(b => b.TaxCode)
            .IsUnique();

        builder.Property(b => b.Address)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(b => b.ContactEmail)
            .HasMaxLength(255);

        builder.Property(b => b.PhoneNumber)
            .HasMaxLength(50);

        builder.Property(b => b.IsActive)
            .IsRequired();

        builder.Property(b => b.CreatedAt)
            .IsRequired();

        builder.Property(b => b.UpdatedAt)
            .IsRequired(false);

        builder.HasMany(b => b.Brands)
            .WithOne(br => br.Business)
            .HasForeignKey(br => br.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
