using NonCash.Core.Interfaces;

namespace NonCash.Core.Entities;

/// <summary>
/// One persisted batch distribution run (Story 3.1 batch promotion). Each promotion run
/// creates exactly one row here so its result (who/when/how many/which recipients failed)
/// stays reviewable later; individual voucher rows link back via VoucherDistribution.BatchId.
/// Single-voucher flows (sale, transfer) do NOT create batch rows.
/// </summary>
public class VoucherDistributionBatch : BaseEntity
{
    public Guid PlanId { get; set; }
    public Guid BrandId { get; set; }

    /// <summary>Staff user who executed the run. Null when triggered via the Integration API.</summary>
    public Guid? CreatedById { get; set; }

    /// <summary>Notification channel requested for this run.</summary>
    public NotificationChannel NotifyChannel { get; set; }

    /// <summary>Number of valid (eligible) recipients the run attempted to send to.</summary>
    public int RecipientCount { get; set; }

    /// <summary>Vouchers actually assigned in this run.</summary>
    public int DistributedCount { get; set; }

    /// <summary>Recipients skipped before distribution (invalid phone, blacklisted, brand-blocked, ...).</summary>
    public int SkippedCount { get; set; }

    /// <summary>Skipped recipient detail (phone/email token + reason), stored as jsonb.</summary>
    public List<BatchSkippedRecipient> SkippedRecords { get; set; } = new();

    // Navigation properties
    public VoucherPlanHeader? Plan { get; set; }
    public UserAccount? CreatedBy { get; set; }
}

/// <summary>A recipient skipped by a batch run. Serialized into VoucherDistributionBatch.SkippedRecords.</summary>
public class BatchSkippedRecipient
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
