using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class VoucherReviewConfiguration : IEntityTypeConfiguration<VoucherReview>
{
    public void Configure(EntityTypeBuilder<VoucherReview> builder)
    {
        builder.ToTable("voucher_reviews");

        builder.Property(r => r.ReviewDate).IsRequired();
        builder.Property(r => r.Decision).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.ReviewNotes).HasColumnType("text");

        builder.HasIndex(r => r.PlanId).HasDatabaseName("IX_voucher_reviews_plan_id");
        builder.HasIndex(r => r.ApproverId).HasDatabaseName("IX_voucher_reviews_approver_id");

        builder.HasOne(r => r.Plan)
            .WithMany()
            .HasForeignKey(r => r.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Approver)
            .WithMany()
            .HasForeignKey(r => r.ApproverId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
