using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NonCash.Core.Entities;
using System.Text.Json;

namespace NonCash.Infrastructure.Data.Configurations;

public class VoucherDistributionBatchConfiguration : IEntityTypeConfiguration<VoucherDistributionBatch>
{
    // SkippedRecords is persisted as a single jsonb column via a value converter — the same
    // provider-agnostic pattern as VoucherPlanHeader.Scope (real jsonb on Npgsql, text on
    // the SQLite/InMemory test providers). PascalCase keys match System.Text.Json defaults.
    private static readonly JsonSerializerOptions SkippedJsonOptions = new(JsonSerializerDefaults.General);

    private static readonly ValueConverter<List<BatchSkippedRecipient>, string> SkippedConverter = new(
        v => JsonSerializer.Serialize(v, SkippedJsonOptions),
        v => string.IsNullOrWhiteSpace(v)
            ? new List<BatchSkippedRecipient>()
            : JsonSerializer.Deserialize<List<BatchSkippedRecipient>>(v, SkippedJsonOptions) ?? new List<BatchSkippedRecipient>());

    private static readonly ValueComparer<List<BatchSkippedRecipient>> SkippedComparer = new(
        (a, b) => JsonSerializer.Serialize(a, SkippedJsonOptions) == JsonSerializer.Serialize(b, SkippedJsonOptions),
        v => JsonSerializer.Serialize(v, SkippedJsonOptions).GetHashCode(),
        v => JsonSerializer.Deserialize<List<BatchSkippedRecipient>>(JsonSerializer.Serialize(v, SkippedJsonOptions), SkippedJsonOptions)!);

    public void Configure(EntityTypeBuilder<VoucherDistributionBatch> builder)
    {
        builder.ToTable("voucher_distribution_batches");

        builder.Property(b => b.PlanId).IsRequired();
        builder.Property(b => b.BrandId).IsRequired();
        builder.Property(b => b.NotifyChannel).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(b => b.RecipientCount).IsRequired();
        builder.Property(b => b.DistributedCount).IsRequired();
        builder.Property(b => b.SkippedCount).IsRequired();

        builder.Property(b => b.SkippedRecords)
            .HasColumnType("jsonb")
            .HasConversion(SkippedConverter, SkippedComparer);

        builder.HasIndex(b => b.PlanId).HasDatabaseName("IX_voucher_distribution_batches_plan_id");
        builder.HasIndex(b => b.BrandId).HasDatabaseName("IX_voucher_distribution_batches_brand_id");

        builder.HasOne(b => b.Plan)
            .WithMany()
            .HasForeignKey(b => b.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.CreatedBy)
            .WithMany()
            .HasForeignKey(b => b.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
