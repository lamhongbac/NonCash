using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class VoucherUsageConfiguration : IEntityTypeConfiguration<VoucherUsage>
{
    public void Configure(EntityTypeBuilder<VoucherUsage> builder)
    {
        builder.ToTable("voucher_usages");

        builder.Property(u => u.TransactionId).HasMaxLength(100).IsRequired();
        builder.Property(u => u.AmountUsed).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(u => u.UsageDate).IsRequired();

        builder.HasIndex(u => u.VoucherId).HasDatabaseName("IX_voucher_usages_voucher_id");
        builder.HasIndex(u => u.TransactionId).IsUnique().HasDatabaseName("IX_voucher_usages_transaction_id");
        builder.HasIndex(u => u.PosId).HasDatabaseName("IX_voucher_usages_pos_id");

        builder.HasOne(u => u.Voucher)
            .WithMany()
            .HasForeignKey(u => u.VoucherId)
            .OnDelete(DeleteBehavior.Restrict);

        // Epic 7.1: Cross-tenant settlement attribution
        builder.Property(u => u.SponsorBrandId);
        builder.Property(u => u.RedeemBrandId);

        builder.HasOne(u => u.SponsorBrand)
            .WithMany()
            .HasForeignKey(u => u.SponsorBrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.RedeemBrand)
            .WithMany()
            .HasForeignKey(u => u.RedeemBrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(u => u.SponsorBrandId).HasDatabaseName("IX_voucher_usages_sponsor_brand_id");
        builder.HasIndex(u => u.RedeemBrandId).HasDatabaseName("IX_voucher_usages_redeem_brand_id");
    }
}
