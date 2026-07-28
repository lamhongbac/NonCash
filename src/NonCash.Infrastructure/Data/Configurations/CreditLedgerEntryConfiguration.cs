using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class CreditLedgerEntryConfiguration : IEntityTypeConfiguration<CreditLedgerEntry>
{
    public void Configure(EntityTypeBuilder<CreditLedgerEntry> builder)
    {
        builder.ToTable("credit_ledger_entries");

        builder.Property(c => c.BrandId).IsRequired();
        builder.Property(c => c.EntryType).IsRequired();
        builder.Property(c => c.Amount).IsRequired();
        builder.Property(c => c.Reference).HasMaxLength(500);

        // 1 voucher = max 1 credit: unique when a Consumption entry references a voucher.
        builder.HasIndex(c => c.VoucherDetailId)
            .IsUnique()
            .HasFilter("voucher_detail_id IS NOT NULL")
            .HasDatabaseName("IX_credit_ledger_entries_voucher_detail_id");

        builder.HasIndex(c => new { c.BrandId, c.CreatedAt })
            .HasDatabaseName("IX_credit_ledger_entries_brand_id_created_at");

        builder.HasOne(c => c.Brand)
            .WithMany()
            .HasForeignKey(c => c.BrandId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
