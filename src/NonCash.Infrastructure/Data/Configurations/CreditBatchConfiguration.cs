using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class CreditBatchConfiguration : IEntityTypeConfiguration<CreditBatch>
{
    public void Configure(EntityTypeBuilder<CreditBatch> builder)
    {
        builder.ToTable("credit_batches");

        builder.Property(b => b.BrandId).IsRequired();
        builder.Property(b => b.BatchType).IsRequired();
        builder.Property(b => b.OriginalAmount).IsRequired();
        builder.Property(b => b.RemainingAmount).IsRequired();
        builder.Property(b => b.PricePerCreditVnd).HasPrecision(18, 2);
        builder.Property(b => b.TotalPaidVnd).HasPrecision(18, 2);
        builder.Property(b => b.EvidenceImageUrl).HasMaxLength(1000);
        builder.Property(b => b.Reference).HasMaxLength(500);

        // FIFO consumption scan: oldest non-empty batches per brand.
        builder.HasIndex(b => new { b.BrandId, b.ExpiresAt })
            .HasDatabaseName("IX_credit_batches_brand_id_expires_at");

        builder.HasIndex(b => new { b.BrandId, b.CreatedAt })
            .HasDatabaseName("IX_credit_batches_brand_id_created_at");

        builder.HasOne(b => b.Brand)
            .WithMany()
            .HasForeignKey(b => b.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Policy)
            .WithMany()
            .HasForeignKey(b => b.PolicyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.AdjustmentRequest)
            .WithMany()
            .HasForeignKey(b => b.AdjustmentRequestId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CreditConsumptionConfiguration : IEntityTypeConfiguration<CreditConsumption>
{
    public void Configure(EntityTypeBuilder<CreditConsumption> builder)
    {
        builder.ToTable("credit_consumptions");

        builder.Property(c => c.BatchId).IsRequired();
        builder.Property(c => c.BrandId).IsRequired();
        builder.Property(c => c.VoucherDetailId).IsRequired();
        builder.Property(c => c.Reference).HasMaxLength(500);

        // 1 voucher = max 1 credit, ever.
        builder.HasIndex(c => c.VoucherDetailId)
            .IsUnique()
            .HasDatabaseName("IX_credit_consumptions_voucher_detail_id");

        builder.HasIndex(c => new { c.BrandId, c.CreatedAt })
            .HasDatabaseName("IX_credit_consumptions_brand_id_created_at");

        builder.HasOne(c => c.Batch)
            .WithMany()
            .HasForeignKey(c => c.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Brand)
            .WithMany()
            .HasForeignKey(c => c.BrandId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CreditExpiryLogConfiguration : IEntityTypeConfiguration<CreditExpiryLog>
{
    public void Configure(EntityTypeBuilder<CreditExpiryLog> builder)
    {
        builder.ToTable("credit_expiry_logs");

        builder.Property(e => e.BatchId).IsRequired();
        builder.Property(e => e.BrandId).IsRequired();
        builder.Property(e => e.ExpiredCredits).IsRequired();
        builder.Property(e => e.ExpiredAt).IsRequired();

        // One expiry event per batch.
        builder.HasIndex(e => e.BatchId)
            .IsUnique()
            .HasDatabaseName("IX_credit_expiry_logs_batch_id");

        builder.HasOne(e => e.Batch)
            .WithMany()
            .HasForeignKey(e => e.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Brand)
            .WithMany()
            .HasForeignKey(e => e.BrandId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
