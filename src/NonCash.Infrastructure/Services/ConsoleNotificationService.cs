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
}
