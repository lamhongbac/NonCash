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

/// <summary>Payload sent to a brand after welcome credits are granted on activation.</summary>
public record WelcomeCreditGrantedNotification(
    string? BrandEmail,
    string BrandName,
    int CreditsGranted,
    DateTime? ExpiresAt);

/// <summary>Payload sent to a brand contact when the business/brand is created by an admin.</summary>
public record BrandCreatedNotification(
    string? BrandEmail,
    string BrandName,
    string BusinessName,
    string TaxCode);

/// <summary>Payload sent to the business contact when its self-registration is approved (business activated).</summary>
public record BusinessActivatedNotification(
    string? BusinessEmail,
    string BusinessName,
    string BrandName,
    int CreditsGranted,
    DateTime? ExpiresAt);

/// <summary>Payload sent to the business contact when the contract is sent for signature.</summary>
public record ContractSentNotification(
    string? BusinessEmail,
    string BusinessName,
    string BrandName,
    string PolicyTemplateName,
    int WelcomeCredits,
    int? WelcomeCreditExpiryMonths,
    string ContractHtml);

/// <summary>Payload sent to a brand after a credit purchase batch is created.</summary>
public record CreditPurchasedNotification(
    string? BrandEmail,
    string BrandName,
    int Amount,
    decimal TotalPaidVnd,
    DateTime? ExpiresAt,
    string? Reference);

/// <summary>Payload sent to a brand when its remaining credit balance drops below the warning threshold.</summary>
public record LowCreditBalanceNotification(
    string? BrandEmail,
    string BrandName,
    int CurrentBalance,
    int Threshold,
    int? TotalGranted);

/// <summary>Payload sent to a brand after credits are forfeited due to batch expiry.</summary>
public record CreditsForfeitedNotification(
    string? BrandEmail,
    string BrandName,
    int ForfeitedCredits,
    DateTime ExpiredAt);

/// <summary>Payload sent to a voucher plan creator after the plan is approved or rejected.</summary>
public record PlanReviewedNotification(
    string? CreatorEmail,
    string PlanDisplayName,
    bool Approved,
    string? ReviewNotes,
    DateTime? PublishDate);

/// <summary>Payload sent to a staff user when their account is created by an admin.</summary>
public record StaffAccountCreatedNotification(
    string? UserEmail,
    string Username,
    string FullName,
    string Role,
    string? BrandName);

/// <summary>Payload sent to a voucher recipient when a transfer is initiated.</summary>
public record VoucherTransferInitiatedNotification(
    string? RecipientEmail,
    string RecipientPhone,
    string RecipientName,
    string SenderName,
    int VoucherCount,
    DateTime TransferredAt);

/// <summary>Payload sent to a user who requested a password reset.</summary>
public record PasswordResetNotification(
    string? UserEmail,
    string FullName,
    string ResetToken,
    DateTime TokenExpiry);

public interface INotificationService
{
    Task NotifyAdminNewRegistrationAsync(Guid requestId, string companyName, CancellationToken cancellationToken = default);
    Task NotifyRegistrationRejectedAsync(Guid userId, string brandName, string? reviewNotes = null, CancellationToken cancellationToken = default);
    Task NotifyApplicantRegistrationSubmittedAsync(string email, string companyName, Guid requestId, CancellationToken cancellationToken = default);
    Task NotifyVoucherReceivedAsync(VoucherReceivedNotification notification, CancellationToken cancellationToken = default);
    Task NotifyAdjustmentPendingAsync(AdjustmentPendingNotification notification, CancellationToken cancellationToken = default);
    Task NotifyAdjustmentReviewedAsync(AdjustmentReviewedNotification notification, CancellationToken cancellationToken = default);
    Task NotifyCreditsExpiringAsync(CreditsExpiringNotification notification, CancellationToken cancellationToken = default);
    Task NotifyWelcomeCreditGrantedAsync(WelcomeCreditGrantedNotification notification, CancellationToken cancellationToken = default);
    Task NotifyBrandCreatedAsync(BrandCreatedNotification notification, CancellationToken cancellationToken = default);
    Task NotifyBusinessActivatedAsync(BusinessActivatedNotification notification, CancellationToken cancellationToken = default);
    Task NotifyContractSentAsync(ContractSentNotification notification, CancellationToken cancellationToken = default);
    Task NotifyCreditPurchasedAsync(CreditPurchasedNotification notification, CancellationToken cancellationToken = default);
    Task NotifyLowCreditBalanceAsync(LowCreditBalanceNotification notification, CancellationToken cancellationToken = default);
    Task NotifyCreditsForfeitedAsync(CreditsForfeitedNotification notification, CancellationToken cancellationToken = default);
    Task NotifyPlanReviewedAsync(PlanReviewedNotification notification, CancellationToken cancellationToken = default);
    Task NotifyStaffAccountCreatedAsync(StaffAccountCreatedNotification notification, CancellationToken cancellationToken = default);
    Task NotifyVoucherTransferInitiatedAsync(VoucherTransferInitiatedNotification notification, CancellationToken cancellationToken = default);
    Task NotifyPasswordResetAsync(PasswordResetNotification notification, CancellationToken cancellationToken = default);
}
