using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class CreditAdjustmentRequestConfiguration : IEntityTypeConfiguration<CreditAdjustmentRequest>
{
    public void Configure(EntityTypeBuilder<CreditAdjustmentRequest> builder)
    {
        builder.ToTable("credit_adjustment_requests");

        builder.Property(a => a.BrandId).IsRequired();
        builder.Property(a => a.AdjustmentType).IsRequired();
        builder.Property(a => a.Amount).IsRequired();
        builder.Property(a => a.ReasonText).IsRequired().HasMaxLength(1000);
        builder.Property(a => a.EvidenceNote).HasMaxLength(1000);
        builder.Property(a => a.EvidenceImageUrl).HasMaxLength(1000);
        builder.Property(a => a.ReviewNote).HasMaxLength(1000);
        builder.Property(a => a.RequestedBy).IsRequired();
        builder.Property(a => a.RequestedAt).IsRequired();

        // Approval queue: pending items, newest first.
        builder.HasIndex(a => new { a.Status, a.RequestedAt })
            .HasDatabaseName("IX_credit_adjustment_requests_status_requested_at");

        builder.HasIndex(a => new { a.BrandId, a.CreatedAt })
            .HasDatabaseName("IX_credit_adjustment_requests_brand_id_created_at");

        builder.HasOne(a => a.Brand)
            .WithMany()
            .HasForeignKey(a => a.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.RelatedBatch)
            .WithMany()
            .HasForeignKey(a => a.RelatedBatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Policy)
            .WithMany()
            .HasForeignKey(a => a.PolicyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
