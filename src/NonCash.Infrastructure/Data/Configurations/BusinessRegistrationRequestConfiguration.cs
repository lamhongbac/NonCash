using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class BusinessRegistrationRequestConfiguration : IEntityTypeConfiguration<BusinessRegistrationRequest>
{
    public void Configure(EntityTypeBuilder<BusinessRegistrationRequest> builder)
    {
        builder.HasKey(r => r.Id);

        // Business information captured at submission time.
        builder.Property(r => r.BusinessName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.TaxCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(r => r.ContactEmail)
            .HasMaxLength(255);

        builder.Property(r => r.PhoneNumber)
            .HasMaxLength(50);

        builder.Property(r => r.Address)
            .HasMaxLength(500);

        builder.Property(r => r.RepresentativeName)
            .IsRequired()
            .HasMaxLength(200);

        // Optional first-brand declaration.
        builder.Property(r => r.FirstBrandName)
            .HasMaxLength(200);

        builder.Property(r => r.ManagerUsername)
            .HasMaxLength(100);

        builder.Property(r => r.ManagerPasswordHash)
            .HasMaxLength(500);

        // Real entities are created only on approval; references are optional until then.
        builder.Property(r => r.BusinessId);
        builder.HasOne(r => r.Business)
            .WithMany()
            .HasForeignKey(r => r.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.BusinessId);

        builder.Property(r => r.BrandId);
        builder.HasOne(r => r.Brand)
            .WithMany()
            .HasForeignKey(r => r.BrandId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.BrandId);

        builder.Property(r => r.SubmittedByUserId);
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

        // Contract workflow fields
        builder.Property(r => r.ContractStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(r => r.ContractStatus);

        builder.Property(r => r.ContractSentAt);

        builder.Property(r => r.ContractFileUrl)
            .HasMaxLength(500);

        builder.HasOne(r => r.WelcomePolicyTemplate)
            .WithMany()
            .HasForeignKey(r => r.WelcomePolicyTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.WelcomePolicyTemplateId);

        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired(false);
    }
}
