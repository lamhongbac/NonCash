using NonCash.Core.Interfaces;

namespace NonCash.Infrastructure.Services;

public class ConsoleNotificationService : INotificationService
{
    public Task NotifyAdminNewRegistrationAsync(Guid requestId, string companyName, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[NOTIFICATION] New registration request #{requestId} for '{companyName}' submitted. Awaiting admin review.");
        return Task.CompletedTask;
    }

    public Task NotifyApplicantReviewResultAsync(Guid userId, string brandName, bool approved, string? reviewNotes = null, CancellationToken cancellationToken = default)
    {
        var status = approved ? "APPROVED" : "REJECTED";
        Console.WriteLine($"[NOTIFICATION] Registration for '{brandName}' has been {status}. User #{userId} notified. Note: {reviewNotes ?? "n/a"}");
        return Task.CompletedTask;
    }

    public Task NotifyApplicantRegistrationSubmittedAsync(string email, string companyName, Guid requestId, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[NOTIFICATION] Thank-you email sent to '{email}' for company '{companyName}'. Request #{requestId}.");
        return Task.CompletedTask;
    }

    public Task NotifyVoucherReceivedAsync(VoucherReceivedNotification notification, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[NOTIFICATION] Voucher '{notification.VoucherName}' ({notification.FaceValue:N0}) delivered to {notification.PhoneNumber} " +
                          $"(email: {notification.Email ?? "n/a"}) via {notification.Channels}. Expires {notification.ExpiryDate:yyyy-MM-dd}.");
        return Task.CompletedTask;
    }

    public Task NotifyAdjustmentPendingAsync(AdjustmentPendingNotification notification, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[NOTIFICATION] Credit adjustment #{notification.RequestId} ({notification.AdjustmentType} {notification.Amount:N0} for '{notification.BrandName}') " +
                          $"requested by {notification.RequestedByName} awaits approval. Notified: {string.Join(", ", notification.ApproverEmails)}.");
        return Task.CompletedTask;
    }

    public Task NotifyAdjustmentReviewedAsync(AdjustmentReviewedNotification notification, CancellationToken cancellationToken = default)
    {
        var outcome = notification.Approved ? "APPROVED" : "REJECTED";
        Console.WriteLine($"[NOTIFICATION] Credit adjustment #{notification.RequestId} ({notification.AdjustmentType} {notification.Amount:N0} for '{notification.BrandName}') " +
                          $"{outcome}. Note: {notification.ReviewNote ?? "n/a"}. Requester notified at {notification.RequesterEmail ?? "n/a"}.");
        return Task.CompletedTask;
    }

    public Task NotifyCreditsExpiringAsync(CreditsExpiringNotification notification, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[NOTIFICATION] '{notification.BrandName}': {notification.ExpiringCredits:N0} credit(s) expire in {notification.DaysLeft} day(s) " +
                          $"on {notification.ExpiresAt:yyyy-MM-dd}. Email: {notification.BrandEmail ?? "n/a"}.");
        return Task.CompletedTask;
    }

    public Task NotifyWelcomeCreditGrantedAsync(WelcomeCreditGrantedNotification notification, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[NOTIFICATION] '{notification.BrandName}' granted {notification.CreditsGranted:N0} welcome credit(s). Email: {notification.BrandEmail ?? "n/a"}.");
        return Task.CompletedTask;
    }

    public Task NotifyCreditPurchasedAsync(CreditPurchasedNotification notification, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[NOTIFICATION] '{notification.BrandName}' purchased {notification.Amount:N0} credit(s) for {notification.TotalPaidVnd:N0} VND. Email: {notification.BrandEmail ?? "n/a"}.");
        return Task.CompletedTask;
    }

    public Task NotifyLowCreditBalanceAsync(LowCreditBalanceNotification notification, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[NOTIFICATION] '{notification.BrandName}' low balance: {notification.CurrentBalance:N0} remaining (threshold {notification.Threshold:N0}). Email: {notification.BrandEmail ?? "n/a"}.");
        return Task.CompletedTask;
    }

    public Task NotifyCreditsForfeitedAsync(CreditsForfeitedNotification notification, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[NOTIFICATION] '{notification.BrandName}' forfeited {notification.ForfeitedCredits:N0} credit(s) on {notification.ExpiredAt:yyyy-MM-dd}. Email: {notification.BrandEmail ?? "n/a"}.");
        return Task.CompletedTask;
    }

    public Task NotifyPlanReviewedAsync(PlanReviewedNotification notification, CancellationToken cancellationToken = default)
    {
        var outcome = notification.Approved ? "APPROVED" : "REJECTED";
        Console.WriteLine($"[NOTIFICATION] Plan '{notification.PlanDisplayName}' {outcome}. Email: {notification.CreatorEmail ?? "n/a"}.");
        return Task.CompletedTask;
    }
}
