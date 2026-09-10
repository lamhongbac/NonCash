using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NonCash.Core.Interfaces;

namespace NonCash.Infrastructure.Services;

/// <summary>
/// CR-2026-09-10-30 row 8: notification sink used when real email delivery is off.
/// Writes one structured line per notification — including magic-link and sign-in URLs,
/// which the email audit table does not store — to a rolling text file so a developer can
/// copy a link straight out of it. Registered scoped, so the resolved path is logged each
/// time a request scope first builds the sink.
/// </summary>
public class FileNotificationService : INotificationService
{
    private static readonly SemaphoreSlim WriteGate = new(1, 1);

    private readonly ILogger _logger;

    public FileNotificationService(IConfiguration? configuration = null, ILogger<FileNotificationService>? logger = null)
    {
        _logger = logger ?? NullLogger<FileNotificationService>.Instance;
        LogPath = ResolveLogPath(configuration?["Notifications:LogFile"]);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            _logger.LogInformation("Notification file sink active. Notifications are appended to {LogPath}.", LogPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Notification file sink could not create {LogPath}. Notifications will be dropped.", LogPath);
        }
    }

    public string LogPath { get; }

    private static string ResolveLogPath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return Path.GetFullPath(configuredPath, Directory.GetCurrentDirectory());

        return Path.Combine(AppContext.BaseDirectory, "logs", "notifications.log");
    }

    public Task NotifyAdminNewRegistrationAsync(Guid requestId, string companyName, CancellationToken cancellationToken = default) =>
        WriteAsync("AdminNewRegistration", $"requestId={requestId} company=\"{companyName}\" action=awaiting-admin-review");

    public Task NotifyRegistrationRejectedAsync(string email, string businessName, string? reviewNotes = null, CancellationToken cancellationToken = default) =>
        WriteAsync("RegistrationRejected", $"to={email ?? "n/a"} business=\"{businessName}\" reason=\"{reviewNotes ?? "n/a"}\"");

    public Task NotifyApplicantRegistrationSubmittedAsync(string email, string companyName, Guid requestId, CancellationToken cancellationToken = default) =>
        WriteAsync("ApplicantRegistrationSubmitted", $"to={email ?? "n/a"} company=\"{companyName}\" requestId={requestId}");

    public Task NotifyVoucherReceivedAsync(VoucherReceivedNotification notification, CancellationToken cancellationToken = default) =>
        WriteAsync("VoucherReceived",
            $"to={notification.Email ?? "n/a"} phone={notification.PhoneNumber} recipient=\"{notification.RecipientName}\" " +
            $"voucher=\"{notification.VoucherName}\" faceValue={notification.FaceValue:N0} expires={notification.ExpiryDate:yyyy-MM-dd} " +
            $"channels={notification.Channels}" + Link("magicLink", notification.MagicLinkUrl));

    public Task NotifyAdjustmentPendingAsync(AdjustmentPendingNotification notification, CancellationToken cancellationToken = default) =>
        WriteAsync("AdjustmentPending",
            $"requestId={notification.RequestId} brand=\"{notification.BrandName}\" type={notification.AdjustmentType} " +
            $"amount={notification.Amount:N0} requestedBy=\"{notification.RequestedByName}\" approvers=[{string.Join(", ", notification.ApproverEmails)}]");

    public Task NotifyAdjustmentReviewedAsync(AdjustmentReviewedNotification notification, CancellationToken cancellationToken = default) =>
        WriteAsync("AdjustmentReviewed",
            $"requestId={notification.RequestId} brand=\"{notification.BrandName}\" type={notification.AdjustmentType} " +
            $"amount={notification.Amount:N0} outcome={(notification.Approved ? "approved" : "rejected")} " +
            $"note=\"{notification.ReviewNote ?? "n/a"}\" to={notification.RequesterEmail ?? "n/a"}");

    public Task NotifyCreditsExpiringAsync(CreditsExpiringNotification notification, CancellationToken cancellationToken = default) =>
        WriteAsync("CreditsExpiring",
            $"to={notification.BrandEmail ?? "n/a"} brand=\"{notification.BrandName}\" expiring={notification.ExpiringCredits:N0} " +
            $"expires={notification.ExpiresAt:yyyy-MM-dd} daysLeft={notification.DaysLeft}");

    public Task NotifyWelcomeCreditGrantedAsync(WelcomeCreditGrantedNotification notification, CancellationToken cancellationToken = default) =>
        WriteAsync("WelcomeCreditGranted",
            $"to={notification.BrandEmail ?? "n/a"} brand=\"{notification.BrandName}\" credits={notification.CreditsGranted:N0} " +
            $"expires={(notification.ExpiresAt.HasValue ? notification.ExpiresAt.Value.ToString("yyyy-MM-dd") : "n/a")}");

    public Task NotifyBrandCreatedAsync(BrandCreatedNotification notification, CancellationToken cancellationToken = default) =>
        WriteAsync("BrandCreated",
            $"to={notification.BrandEmail ?? "n/a"} brand=\"{notification.BrandName}\" business=\"{notification.BusinessName}\" taxCode={notification.TaxCode}");

    public Task NotifyBusinessActivatedAsync(BusinessActivatedNotification notification, CancellationToken cancellationToken = default) =>
        WriteAsync("BusinessActivated",
            $"to={notification.BusinessEmail ?? "n/a"} business=\"{notification.BusinessName}\" brand=\"{notification.BrandName}\" credits={notification.CreditsGranted:N0}");

    public Task NotifyContractSentAsync(ContractSentNotification notification, CancellationToken cancellationToken = default) =>
        WriteAsync("ContractSent",
            $"requestId={notification.RequestId} to={notification.BusinessEmail ?? "n/a"} business=\"{notification.BusinessName}\" " +
            $"brand=\"{notification.BrandName}\" policy=\"{notification.PolicyTemplateName}\" credits={notification.WelcomeCredits:N0} " +
            $"confirmationToken={notification.ConfirmationToken}");

    public Task NotifyCreditPurchasedAsync(CreditPurchasedNotification notification, CancellationToken cancellationToken = default) =>
        WriteAsync("CreditPurchased",
            $"to={notification.BrandEmail ?? "n/a"} brand=\"{notification.BrandName}\" credits={notification.Amount:N0} " +
            $"paidVnd={notification.TotalPaidVnd:N0} reference=\"{notification.Reference ?? "n/a"}\"");

    public Task NotifyLowCreditBalanceAsync(LowCreditBalanceNotification notification, CancellationToken cancellationToken = default) =>
        WriteAsync("LowCreditBalance",
            $"to={notification.BrandEmail ?? "n/a"} brand=\"{notification.BrandName}\" balance={notification.CurrentBalance:N0} threshold={notification.Threshold:N0}");

    public Task NotifyCreditsForfeitedAsync(CreditsForfeitedNotification notification, CancellationToken cancellationToken = default) =>
        WriteAsync("CreditsForfeited",
            $"to={notification.BrandEmail ?? "n/a"} brand=\"{notification.BrandName}\" forfeited={notification.ForfeitedCredits:N0} expiredAt={notification.ExpiredAt:yyyy-MM-dd}");

    public Task NotifyPlanReviewedAsync(PlanReviewedNotification notification, CancellationToken cancellationToken = default) =>
        WriteAsync("PlanReviewed",
            $"to={notification.CreatorEmail ?? "n/a"} plan=\"{notification.PlanDisplayName}\" outcome={(notification.Approved ? "approved" : "rejected")} " +
            $"notes=\"{notification.ReviewNotes ?? "n/a"}\"");

    public Task NotifyStaffAccountCreatedAsync(StaffAccountCreatedNotification notification, CancellationToken cancellationToken = default) =>
        WriteAsync("StaffAccountCreated",
            $"to={notification.UserEmail ?? "n/a"} username={notification.Username} name=\"{notification.FullName}\" role={notification.Role} brand=\"{notification.BrandName ?? "n/a"}\"");

    public Task NotifyVoucherTransferInitiatedAsync(VoucherTransferInitiatedNotification notification, CancellationToken cancellationToken = default) =>
        WriteAsync("GiftSent",
            $"to={notification.RecipientEmail ?? "n/a"} phone={notification.RecipientPhone} recipient=\"{notification.RecipientName}\" " +
            $"sender=\"{notification.SenderName}\" voucherCount={notification.VoucherCount} sentAt={notification.TransferredAt:yyyy-MM-dd HH:mm}" +
            Link("magicLink", notification.MagicLinkUrl));

    public Task NotifyGiftAcceptedAsync(GiftAcceptedNotification notification, CancellationToken cancellationToken = default) =>
        WriteAsync("GiftAccepted",
            $"to={notification.SenderEmail ?? "n/a"} sender=\"{notification.SenderName}\" recipient=\"{notification.RecipientName}\" " +
            $"voucher=\"{notification.VoucherName}\" faceValue={notification.FaceValue:N0} acceptedAt={notification.AcceptedAt:yyyy-MM-dd HH:mm} " +
            $"thankYouNote=\"{notification.RecipientThankYouNote ?? "n/a"}\"");

    public Task NotifyGiftDeclinedAsync(GiftDeclinedNotification notification, CancellationToken cancellationToken = default) =>
        WriteAsync("GiftDeclined",
            $"to={notification.SenderEmail ?? "n/a"} sender=\"{notification.SenderName}\" recipient=\"{notification.RecipientName}\" " +
            $"voucher=\"{notification.VoucherName}\" faceValue={notification.FaceValue:N0} declinedAt={notification.DeclinedAt:yyyy-MM-dd HH:mm} " +
            $"reason=\"{notification.Reason ?? "n/a"}\"");

    public Task NotifyGiftExpiredAsync(GiftExpiredNotification notification, CancellationToken cancellationToken = default) =>
        WriteAsync("GiftExpired",
            $"to={notification.SenderEmail ?? "n/a"} sender=\"{notification.SenderName}\" recipient=\"{notification.RecipientName}\" " +
            $"phone={notification.RecipientPhone} voucher=\"{notification.VoucherName}\" faceValue={notification.FaceValue:N0} " +
            $"heldDays={notification.ExpiryDays} expiredAt={notification.ExpiredAt:yyyy-MM-dd HH:mm}");

    public Task NotifyGiftMessageAsync(GiftMessageNotification notification, CancellationToken cancellationToken = default) =>
        WriteAsync("GiftMessage",
            $"to={notification.RecipientEmail ?? "n/a"} recipient=\"{notification.RecipientName}\" author=\"{notification.AuthorName}\" " +
            $"voucher=\"{notification.VoucherName}\" faceValue={notification.FaceValue:N0} sentAt={notification.SentAt:yyyy-MM-dd HH:mm} " +
            $"body=\"{notification.Body}\"");

    public Task NotifyPasswordResetAsync(PasswordResetNotification notification, CancellationToken cancellationToken = default) =>
        WriteAsync("PasswordReset",
            $"to={notification.UserEmail ?? "n/a"} name=\"{notification.FullName}\" token={notification.ResetToken} expires={notification.TokenExpiry:yyyy-MM-dd HH:mm}");

    public Task NotifySignInLinkAsync(SignInLinkNotification notification, CancellationToken cancellationToken = default) =>
        WriteAsync("MemberSignInLink",
            $"to={notification.MemberEmail ?? "n/a"} name=\"{notification.FullName}\" expires={notification.LinkExpiry:yyyy-MM-dd HH:mm}" +
            Link("signInLink", notification.SignInLinkUrl));

    private static string Link(string name, string? url) =>
        string.IsNullOrWhiteSpace(url) ? string.Empty : $" {name}={url}";

    private async Task WriteAsync(string category, string message)
    {
        var line = $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ} [{category}] {message}{Environment.NewLine}";

        await WriteGate.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(LogPath, line);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not append the {Category} notification to {LogPath}.", category, LogPath);
        }
        finally
        {
            WriteGate.Release();
        }
    }
}
