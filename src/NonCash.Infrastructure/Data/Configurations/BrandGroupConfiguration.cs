using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class BrandGroupConfiguration : IEntityTypeConfiguration<BrandGroup>
{
    public void Configure(EntityTypeBuilder<BrandGroup> builder)
    {
        builder.ToTable("brand_groups");

        builder.Property(g => g.Name).IsRequired().HasMaxLength(200);
        builder.Property(g => g.Description).HasMaxLength(1000);

        builder.HasIndex(g => g.Name)
            .IsUnique()
            .HasDatabaseName("IX_brand_groups_name");
    }
}

public class BrandGroupMemberConfiguration : IEntityTypeConfiguration<BrandGroupMember>
{
    public void Configure(EntityTypeBuilder<BrandGroupMember> builder)
    {
        builder.ToTable("brand_group_members");

        builder.Property(m => m.BrandGroupId).IsRequired();
        builder.Property(m => m.BrandId).IsRequired();

        // A brand appears at most once per group.
        builder.HasIndex(m => new { m.BrandGroupId, m.BrandId })
            .IsUnique()
            .HasDatabaseName("IX_brand_group_members_group_brand");

        builder.HasOne(m => m.BrandGroup)
            .WithMany(g => g.Members)
            .HasForeignKey(m => m.BrandGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Brand)
            .WithMany()
            .HasForeignKey(m => m.BrandId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
