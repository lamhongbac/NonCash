using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class BrandRegistrationRequestConfiguration : IEntityTypeConfiguration<BrandRegistrationRequest>
{
    public void Configure(EntityTypeBuilder<BrandRegistrationRequest> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.BrandId).IsRequired();
        builder.HasOne(r => r.Brand)
            .WithMany()
            .HasForeignKey(r => r.BrandId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.BrandId);

        builder.Property(r => r.SubmittedByUserId).IsRequired();
        builder.HasOne(r => r.SubmittedBy)
            .WithMany()
            .HasForeignKey(r => r.SubmittedByUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(r => r.SubmittedAt).IsRequired();

        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(r => r.Status);

        builder.Property(r => r.ReviewNotes)
            .HasMaxLength(1000);

        builder.Property(r => r.ReviewedAt);

        builder.HasOne(r => r.ReviewedBy)
            .WithMany()
            .HasForeignKey(r => r.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired(false);
    }
}
