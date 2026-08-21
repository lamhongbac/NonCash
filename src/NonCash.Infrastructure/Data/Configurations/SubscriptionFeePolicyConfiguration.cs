using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class SubscriptionFeePolicyConfiguration : IEntityTypeConfiguration<SubscriptionFeePolicy>
{
    public void Configure(EntityTypeBuilder<SubscriptionFeePolicy> builder)
    {
        builder.ToTable("subscription_fee_policies");

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.AmountVnd).HasPrecision(18, 2);
        builder.Property(p => p.IsFree).HasDefaultValue(false);
        builder.Property(p => p.MinimumCommitmentMonths).HasDefaultValue(12);
        builder.Property(p => p.IsActive).HasDefaultValue(true);

        builder.HasIndex(p => new { p.IsActive, p.EffectiveFrom, p.EffectiveTo })
            .HasDatabaseName("IX_subscription_fee_policies_effective");
    }
}
