using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NonCash.Core.Entities;

namespace NonCash.Infrastructure.Data.Configurations;

public class UserOutletConfiguration : IEntityTypeConfiguration<UserOutlet>
{
    public void Configure(EntityTypeBuilder<UserOutlet> builder)
    {
        builder.ToTable("user_outlet_assignments");

        builder.HasKey(uo => uo.Id);

        builder.Property(uo => uo.UserId).IsRequired();
        builder.Property(uo => uo.OutletId).IsRequired();

        // One row per (user, outlet) pair.
        builder.HasIndex(uo => new { uo.UserId, uo.OutletId }).IsUnique();

        builder.HasIndex(uo => uo.OutletId);

        builder.HasOne(uo => uo.User)
            .WithMany()
            .HasForeignKey(uo => uo.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(uo => uo.Outlet)
            .WithMany()
            .HasForeignKey(uo => uo.OutletId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(uo => uo.CreatedAt).IsRequired();
        builder.Property(uo => uo.UpdatedAt).IsRequired(false);
    }
}
