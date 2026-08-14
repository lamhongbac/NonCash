namespace NonCash.Core.Entities;

/// <summary>
/// Audit trail for outbound email notifications.
/// Every send attempt (success or failure) is recorded for traceability.
/// </summary>
public class EmailLog : BaseEntity
{
    public string ToAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>Free-text description of the notification scenario (e.g. "PlanReviewed", "AdjustmentPending").</summary>
    public string NotificationType { get; set; } = string.Empty;

    /// <summary>Optional related entity ID (e.g. plan ID, adjustment request ID).</summary>
    public Guid? RelatedEntityId { get; set; }

    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime SentAt { get; set; }
}
