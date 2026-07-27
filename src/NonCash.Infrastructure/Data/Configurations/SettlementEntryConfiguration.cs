using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class SettlementEntryConfiguration : IEntityTypeConfiguration<SettlementEntry>
{
    public void Configure(EntityTypeBuilder<SettlementEntry> builder)
    {
        builder.ToTable("settlement_entries");

        builder.Property(s => s.FaceValue).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(s => s.Status).IsRequired();

        builder.HasIndex(s => s.SponsorBrandId).HasDatabaseName("IX_settlement_entries_sponsor_brand_id");
        builder.HasIndex(s => s.IssuingBrandId).HasDatabaseName("IX_settlement_entries_issuing_brand_id");
        builder.HasIndex(s => s.RedeemBrandId).HasDatabaseName("IX_settlement_entries_redeem_brand_id");
        builder.HasIndex(s => s.Status).HasDatabaseName("IX_settlement_entries_status");
        builder.HasIndex(s => s.VoucherUsageId).IsUnique().HasDatabaseName("IX_settlement_entries_voucher_usage_id");
        builder.HasIndex(s => s.CreatedAt).HasDatabaseName("IX_settlement_entries_created_at");

        builder.HasOne(s => s.SponsorBrand)
            .WithMany()
            .HasForeignKey(s => s.SponsorBrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.IssuingBrand)
            .WithMany()
            .HasForeignKey(s => s.IssuingBrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.RedeemBrand)
            .WithMany()
            .HasForeignKey(s => s.RedeemBrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.RedeemOutlet)
            .WithMany()
            .HasForeignKey(s => s.RedeemOutletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.VoucherUsage)
            .WithOne()
            .HasForeignKey<SettlementEntry>(s => s.VoucherUsageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
