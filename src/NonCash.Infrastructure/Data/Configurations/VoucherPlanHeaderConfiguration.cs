using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NonCash.Core.Entities;
using System.Text.Json;

namespace NonCash.Infrastructure.Data.Configurations;

public class VoucherPlanHeaderConfiguration : IEntityTypeConfiguration<VoucherPlanHeader>
{
    // Epic 3: VoucherScope is persisted as a single jsonb column via a value converter.
    // A converter (rather than an owned-entity ToJson mapping) keeps this provider-agnostic:
    // Npgsql stores real jsonb, while the SQLite/InMemory integration-test providers store it
    // as text/in-memory. PascalCase keys match System.Text.Json "General" defaults.
    private static readonly JsonSerializerOptions ScopeJsonOptions = new(JsonSerializerDefaults.General);

    private static readonly ValueConverter<VoucherScope, string> ScopeConverter = new(
        v => JsonSerializer.Serialize(v, ScopeJsonOptions),
        v => string.IsNullOrWhiteSpace(v)
            ? new VoucherScope()
            : JsonSerializer.Deserialize<VoucherScope>(v, ScopeJsonOptions) ?? new VoucherScope());

    private static readonly ValueComparer<VoucherScope> ScopeComparer = new(
        (a, b) => JsonSerializer.Serialize(a, ScopeJsonOptions) == JsonSerializer.Serialize(b, ScopeJsonOptions),
        v => JsonSerializer.Serialize(v, ScopeJsonOptions).GetHashCode(),
        v => JsonSerializer.Deserialize<VoucherScope>(JsonSerializer.Serialize(v, ScopeJsonOptions), ScopeJsonOptions)!);

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

        // Epic 7.1: Cross-tenant sponsorship
        builder.Property(p => p.SponsorBrandId);

        // Epic 8.1: Display data model
        builder.Property(p => p.CoverImageUrl).HasColumnType("text");
        builder.Property(p => p.TermsAndConditions).HasColumnType("text");
        builder.Property(p => p.BrandColor).HasMaxLength(7);
        builder.Property(p => p.DisplayName).HasMaxLength(200);
        builder.Property(p => p.ShortDescription).HasMaxLength(500);
        builder.Property(p => p.ValidDaysOfWeek).HasMaxLength(50);

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

        builder.HasOne(p => p.SponsorBrand)
            .WithMany()
            .HasForeignKey(p => p.SponsorBrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.PreviousVersion)
            .WithMany()
            .HasForeignKey(p => p.PreviousVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Epic 3: applicability scope stored as a single jsonb column ("scope") on the header,
        // serialized via the provider-agnostic converter above (no separate table/join).
        builder.Property(p => p.Scope)
            .HasColumnType("jsonb")
            .HasConversion(ScopeConverter, ScopeComparer);
    }
}
