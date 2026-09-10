using NonCash.Core.Interfaces;

namespace NonCash.IntegrationTests.Fixtures;

/// <summary>
/// Captures every notification a flow produces so tests can assert who was told what
/// (CR-2026-09-10-30: gift accept/decline/expiry messages to the sender).
/// </summary>
public sealed class RecordingNotificationService : INotificationService
{
    public List<VoucherReceivedNotification> VoucherReceived { get; } = new();
    public List<VoucherTransferInitiatedNotification> GiftsSent { get; } = new();
    public List<GiftAcceptedNotification> GiftsAccepted { get; } = new();
    public List<GiftDeclinedNotification> GiftsDeclined { get; } = new();
    public List<GiftExpiredNotification> GiftsExpired { get; } = new();
    public List<GiftMessageNotification> GiftMessages { get; } = new();
    public List<SignInLinkNotification> SignInLinks { get; } = new();
    public List<PasswordResetNotification> PasswordResets { get; } = new();

    public int TotalCount =>
        VoucherReceived.Count + GiftsSent.Count + GiftsAccepted.Count + GiftsDeclined.Count
        + GiftsExpired.Count + GiftMessages.Count + SignInLinks.Count + PasswordResets.Count;

    public Task NotifyAdminNewRegistrationAsync(Guid requestId, string companyName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyRegistrationRejectedAsync(string email, string businessName, string? reviewNotes = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyApplicantRegistrationSubmittedAsync(string email, string companyName, Guid requestId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyAdjustmentPendingAsync(AdjustmentPendingNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyAdjustmentReviewedAsync(AdjustmentReviewedNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyCreditsExpiringAsync(CreditsExpiringNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyWelcomeCreditGrantedAsync(WelcomeCreditGrantedNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyBrandCreatedAsync(BrandCreatedNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyBusinessActivatedAsync(BusinessActivatedNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyContractSentAsync(ContractSentNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyCreditPurchasedAsync(CreditPurchasedNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyLowCreditBalanceAsync(LowCreditBalanceNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyCreditsForfeitedAsync(CreditsForfeitedNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyPlanReviewedAsync(PlanReviewedNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyStaffAccountCreatedAsync(StaffAccountCreatedNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task NotifyVoucherReceivedAsync(VoucherReceivedNotification notification, CancellationToken cancellationToken = default)
    {
        VoucherReceived.Add(notification);
        return Task.CompletedTask;
    }

    public Task NotifyVoucherTransferInitiatedAsync(VoucherTransferInitiatedNotification notification, CancellationToken cancellationToken = default)
    {
        GiftsSent.Add(notification);
        return Task.CompletedTask;
    }

    public Task NotifyGiftAcceptedAsync(GiftAcceptedNotification notification, CancellationToken cancellationToken = default)
    {
        GiftsAccepted.Add(notification);
        return Task.CompletedTask;
    }

    public Task NotifyGiftDeclinedAsync(GiftDeclinedNotification notification, CancellationToken cancellationToken = default)
    {
        GiftsDeclined.Add(notification);
        return Task.CompletedTask;
    }

    public Task NotifyGiftExpiredAsync(GiftExpiredNotification notification, CancellationToken cancellationToken = default)
    {
        GiftsExpired.Add(notification);
        return Task.CompletedTask;
    }

    public Task NotifyGiftMessageAsync(GiftMessageNotification notification, CancellationToken cancellationToken = default)
    {
        GiftMessages.Add(notification);
        return Task.CompletedTask;
    }

    public Task NotifyPasswordResetAsync(PasswordResetNotification notification, CancellationToken cancellationToken = default)
    {
        PasswordResets.Add(notification);
        return Task.CompletedTask;
    }

    public Task NotifySignInLinkAsync(SignInLinkNotification notification, CancellationToken cancellationToken = default)
    {
        SignInLinks.Add(notification);
        return Task.CompletedTask;
    }
}
