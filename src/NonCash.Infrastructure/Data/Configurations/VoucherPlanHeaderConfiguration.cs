using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class VoucherPlanHeaderConfiguration : IEntityTypeConfiguration<VoucherPlanHeader>
{
    public void Configure(EntityTypeBuilder<VoucherPlanHeader> builder)
    {
        builder.ToTable("voucher_plan_headers");

        builder.Property(p => p.PlanDate).IsRequired();
        builder.Property(p => p.CreatorId).IsRequired();
        builder.Property(p => p.BrandId).IsRequired();
        builder.Property(p => p.VoucherType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.ValueType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.FaceValue).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(p => p.NetValue).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(p => p.ExpiryDate).IsRequired();
        builder.Property(p => p.PublishDate).IsRequired();
        builder.Property(p => p.TargetQuantity).IsRequired();
        builder.Property(p => p.Budget).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(p => p.ApprovalStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.ImageUrl).HasColumnType("text");
        builder.Property(p => p.IconUrl).HasColumnType("text");
        builder.Property(p => p.VersionNumber).HasDefaultValue(1).IsRequired();

        builder.HasIndex(p => p.BrandId).HasDatabaseName("IX_voucher_plan_headers_brand_id");
        builder.HasIndex(p => p.ApprovalStatus).HasDatabaseName("IX_voucher_plan_headers_approval_status");
        builder.HasIndex(p => p.PreviousVersionId).HasDatabaseName("IX_voucher_plan_headers_previous_version_id");

        builder.HasOne(p => p.Creator)
            .WithMany()
            .HasForeignKey(p => p.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Approver)
            .WithMany()
            .HasForeignKey(p => p.ApproverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Brand)
            .WithMany()
            .HasForeignKey(p => p.BrandId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.PreviousVersion)
            .WithMany()
            .HasForeignKey(p => p.PreviousVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.PlanOutlets)
            .WithOne(po => po.Plan)
            .HasForeignKey(po => po.PlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PlanOutletConfiguration : IEntityTypeConfiguration<PlanOutlet>
{
    public void Configure(EntityTypeBuilder<PlanOutlet> builder)
    {
        builder.ToTable("plan_outlets");

        builder.HasKey(po => new { po.PlanId, po.OutletId });

        builder.HasOne(po => po.Plan)
            .WithMany(p => p.PlanOutlets)
            .HasForeignKey(po => po.PlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(po => po.Outlet)
            .WithMany()
            .HasForeignKey(po => po.OutletId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
