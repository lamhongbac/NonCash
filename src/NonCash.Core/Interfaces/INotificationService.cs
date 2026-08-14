namespace NonCash.Core.Interfaces;

/// <summary>
/// Delivery channels for member-facing notifications.
/// SMS is intentionally excluded (cost); Zalo ZNS activates once the OA is onboarded.
/// </summary>
[Flags]
public enum NotificationChannel
{
    None = 0,
    Email = 1,
    Zalo = 2,
    Both = Email | Zalo
}

/// <summary>Payload for the voucher-received notification sent after distribution.</summary>
public record VoucherReceivedNotification(
    string? Email,
    string PhoneNumber,
    string RecipientName,
    string? VoucherName,
    decimal FaceValue,
    DateTime ExpiryDate,
    NotificationChannel Channels);

/// <summary>Payload sent to FinancialControllers when an adjustment awaits approval (Epic 10).</summary>
public record AdjustmentPendingNotification(
    Guid RequestId,
    string BrandName,
    string AdjustmentType,
    int Amount,
    string RequestedByName,
    IReadOnlyList<string> ApproverEmails);

/// <summary>Payload sent to the requester after an adjustment is approved or rejected (Epic 10).</summary>
public record AdjustmentReviewedNotification(
    Guid RequestId,
    string BrandName,
    string AdjustmentType,
    int Amount,
    bool Approved,
    string? ReviewNote,
    string? RequesterEmail);

/// <summary>Payload warning a brand that a credit batch expires soon (Epic 10).</summary>
public record CreditsExpiringNotification(
    string? BrandEmail,
    string BrandName,
    int ExpiringCredits,
    DateTime ExpiresAt,
    int DaysLeft);

public interface INotificationService
{
    Task NotifyAdminNewRegistrationAsync(Guid requestId, string companyName, CancellationToken cancellationToken = default);
    Task NotifyApplicantReviewResultAsync(Guid userId, string brandName, bool approved, string? reviewNotes = null, CancellationToken cancellationToken = default);
    Task NotifyApplicantRegistrationSubmittedAsync(string email, string companyName, Guid requestId, CancellationToken cancellationToken = default);
    Task NotifyVoucherReceivedAsync(VoucherReceivedNotification notification, CancellationToken cancellationToken = default);
    Task NotifyAdjustmentPendingAsync(AdjustmentPendingNotification notification, CancellationToken cancellationToken = default);
    Task NotifyAdjustmentReviewedAsync(AdjustmentReviewedNotification notification, CancellationToken cancellationToken = default);
    Task NotifyCreditsExpiringAsync(CreditsExpiringNotification notification, CancellationToken cancellationToken = default);
}
