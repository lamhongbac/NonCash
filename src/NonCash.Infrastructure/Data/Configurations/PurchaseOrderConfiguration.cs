using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("purchase_orders");

        builder.Property(o => o.MemberId).IsRequired();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(o => o.InvoiceCompanyName).HasMaxLength(200);
        builder.Property(o => o.InvoiceTaxCode).HasMaxLength(50);
        builder.Property(o => o.TotalAmount).HasColumnType("numeric(18,2)").IsRequired();

        builder.HasIndex(o => o.MemberId).HasDatabaseName("IX_purchase_orders_member_id");
        builder.HasIndex(o => o.Status).HasDatabaseName("IX_purchase_orders_status");

        builder.HasOne(o => o.Member)
            .WithMany()
            .HasForeignKey(o => o.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.OrderDetails)
            .WithOne(d => d.Order)
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrderDetailConfiguration : IEntityTypeConfiguration<OrderDetail>
{
    public void Configure(EntityTypeBuilder<OrderDetail> builder)
    {
        builder.ToTable("order_details");

        builder.Property(d => d.OrderId).IsRequired();
        builder.Property(d => d.PlanId).IsRequired();
        builder.Property(d => d.Quantity).IsRequired();
        builder.Property(d => d.UnitPrice).HasColumnType("numeric(18,2)").IsRequired();

        builder.HasIndex(d => d.OrderId).HasDatabaseName("IX_order_details_order_id");
        builder.HasIndex(d => d.PlanId).HasDatabaseName("IX_order_details_plan_id");

        builder.HasOne(d => d.Plan)
            .WithMany()
            .HasForeignKey(d => d.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
