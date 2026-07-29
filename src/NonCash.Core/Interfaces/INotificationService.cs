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

public interface INotificationService
{
    Task NotifyAdminNewRegistrationAsync(Guid requestId, string companyName, CancellationToken cancellationToken = default);
    Task NotifyApplicantReviewResultAsync(Guid userId, string brandName, bool approved, CancellationToken cancellationToken = default);
    Task NotifyApplicantRegistrationSubmittedAsync(string email, string companyName, Guid requestId, CancellationToken cancellationToken = default);
    Task NotifyVoucherReceivedAsync(VoucherReceivedNotification notification, CancellationToken cancellationToken = default);
}
