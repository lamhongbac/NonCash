using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class WelcomeGrantPolicyConfiguration : IEntityTypeConfiguration<WelcomeGrantPolicy>
{
    public void Configure(EntityTypeBuilder<WelcomeGrantPolicy> builder)
    {
        builder.ToTable("welcome_grant_policies");

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.BusinessId).IsRequired();
        builder.Property(p => p.EffectiveFrom).IsRequired();

        // Resolution scan: most recent active policy per business.
        builder.HasIndex(p => new { p.BusinessId, p.IsActive, p.EffectiveFrom })
            .HasDatabaseName("IX_welcome_grant_policies_business_active_from");

        builder.HasOne(p => p.Business)
            .WithMany()
            .HasForeignKey(p => p.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
