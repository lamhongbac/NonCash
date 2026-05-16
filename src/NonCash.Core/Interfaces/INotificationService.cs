namespace NonCash.Core.Interfaces;

public interface INotificationService
{
    Task NotifyAdminNewRegistrationAsync(Guid requestId, string companyName, CancellationToken cancellationToken = default);
    Task NotifyApplicantReviewResultAsync(Guid userId, string brandName, bool approved, CancellationToken cancellationToken = default);
}
