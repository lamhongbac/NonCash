using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class VoucherPlanDetailConfiguration : IEntityTypeConfiguration<VoucherPlanDetail>
{
    public void Configure(EntityTypeBuilder<VoucherPlanDetail> builder)
    {
        builder.ToTable("voucher_plan_details");

        builder.Property(v => v.SerialNo).HasMaxLength(50).IsRequired();
        builder.Property(v => v.VoucherCodeSecret).HasMaxLength(255).IsRequired();
        builder.Property(v => v.UsageStatus).HasConversion<string>().HasMaxLength(20).IsRequired();

        // Story 4-2: lock columns
        builder.Property(v => v.BillNumber).HasMaxLength(100);

        builder.HasIndex(v => v.ParentId).HasDatabaseName("IX_voucher_plan_details_parent_id");
        builder.HasIndex(v => v.SerialNo).IsUnique().HasDatabaseName("IX_voucher_plan_details_serial_no");
        builder.HasIndex(v => v.MemberId).HasDatabaseName("IX_voucher_plan_details_member_id");
        builder.HasIndex(v => v.LockId).HasDatabaseName("IX_voucher_plan_details_lock_id");

        builder.HasOne(v => v.Parent)
            .WithMany()
            .HasForeignKey(v => v.ParentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
