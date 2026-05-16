using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class VoucherDistributionConfiguration : IEntityTypeConfiguration<VoucherDistribution>
{
    public void Configure(EntityTypeBuilder<VoucherDistribution> builder)
    {
        builder.ToTable("voucher_distributions");

        builder.Property(d => d.VoucherId).IsRequired();
        builder.Property(d => d.MemberId).IsRequired();
        builder.Property(d => d.Method).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(d => d.DistributionDate).IsRequired();

        builder.HasIndex(d => d.VoucherId).HasDatabaseName("IX_voucher_distributions_voucher_id");
        builder.HasIndex(d => d.MemberId).HasDatabaseName("IX_voucher_distributions_member_id");

        builder.HasOne(d => d.Voucher)
            .WithMany()
            .HasForeignKey(d => d.VoucherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Member)
            .WithMany()
            .HasForeignKey(d => d.MemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
