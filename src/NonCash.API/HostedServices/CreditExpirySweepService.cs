using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.API.HostedServices;

/// <summary>
/// Daily credit batch sweep (Epic 10):
/// 1) zeroes out batches past ExpiresAt, logging forfeited credits to credit_expiry_logs;
/// 2) sends the one-time expiry warning to brands ExpiryWarningDays before a batch expires.
/// </summary>
public class CreditExpirySweepService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CreditExpirySweepService> _logger;
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(24);

    public CreditExpirySweepService(IServiceProvider serviceProvider, ILogger<CreditExpirySweepService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var policyService = scope.ServiceProvider.GetRequiredService<ICreditPolicyService>();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                await ExpireBatchesAsync(db, notificationService, stoppingToken);
                await SendExpiryWarningsAsync(db, policyService, notificationService, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreditExpirySweepService sweep failed.");
            }

            try
            {
                await Task.Delay(SweepInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ExpireBatchesAsync(ApplicationDbContext db, INotificationService notificationService, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var expiredBatches = await db.CreditBatches
            .Where(b => b.RemainingAmount > 0 && b.ExpiresAt != null && b.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        if (expiredBatches.Count == 0)
            return;

        foreach (var batch in expiredBatches)
        {
            db.CreditExpiryLogs.Add(new CreditExpiryLog
            {
                BatchId = batch.Id,
                BrandId = batch.BrandId,
                ExpiredCredits = batch.RemainingAmount,
                ExpiredAt = now
            });
            batch.RemainingAmount = 0;
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("CreditExpirySweepService expired {Count} batch(es).", expiredBatches.Count);

        // Notify brands about forfeited credits.
        var brandIds = expiredBatches.Select(b => b.BrandId).Distinct().ToList();
        var brands = await db.Brands
            .AsNoTracking()
            .Where(b => brandIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, cancellationToken);

        foreach (var group in expiredBatches.GroupBy(b => b.BrandId))
        {
            try
            {
                if (!brands.TryGetValue(group.Key, out var brand))
                    continue;

                var forfeited = group.Sum(b => b.RemainingAmount);
                await notificationService.NotifyCreditsForfeitedAsync(new CreditsForfeitedNotification(
                    brand.ContactEmail, brand.Name, forfeited, now), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send forfeiture notification for brand {BrandId}.", group.Key);
            }
        }
    }

    private async Task SendExpiryWarningsAsync(
        ApplicationDbContext db,
        ICreditPolicyService policyService,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // Candidates: unexpired batches with credits, not yet warned. The per-brand
        // warning window (ExpiryWarningDays) is applied after policy resolution.
        var candidates = await db.CreditBatches
            .Include(b => b.Brand)
            .Where(b => b.RemainingAmount > 0
                && b.ExpiresAt != null
                && b.ExpiresAt > now
                && b.ExpiryWarningSentAt == null)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return;

        var warned = 0;
        foreach (var group in candidates.GroupBy(b => b.BrandId))
        {
            var policy = await policyService.ResolveForBrandAsync(group.Key, cancellationToken);
            if (policy.ExpiryWarningDays is not > 0)
                continue;

            var cutoff = now.AddDays(policy.ExpiryWarningDays.Value);
            foreach (var batch in group.Where(b => b.ExpiresAt <= cutoff))
            {
                var daysLeft = Math.Max(0, (int)Math.Ceiling((batch.ExpiresAt!.Value - now).TotalDays));
                await notificationService.NotifyCreditsExpiringAsync(new CreditsExpiringNotification(
                    batch.Brand?.ContactEmail,
                    batch.Brand?.Name ?? "(unknown brand)",
                    batch.RemainingAmount,
                    batch.ExpiresAt.Value,
                    daysLeft), cancellationToken);

                batch.ExpiryWarningSentAt = now;
                warned++;
            }
        }

        if (warned > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("CreditExpirySweepService sent {Count} expiry warning(s).", warned);
        }
    }
}
