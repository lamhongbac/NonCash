using NonCash.Core.Interfaces;

namespace NonCash.Infrastructure.Services;

public class ConsoleNotificationService : INotificationService
{
    public Task NotifyAdminNewRegistrationAsync(Guid requestId, string companyName, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[NOTIFICATION] New registration request #{requestId} for '{companyName}' submitted. Awaiting admin review.");
        return Task.CompletedTask;
    }

    public Task NotifyApplicantReviewResultAsync(Guid userId, string brandName, bool approved, CancellationToken cancellationToken = default)
    {
        var status = approved ? "APPROVED" : "REJECTED";
        Console.WriteLine($"[NOTIFICATION] Registration for '{brandName}' has been {status}. User #{userId} notified.");
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
}
