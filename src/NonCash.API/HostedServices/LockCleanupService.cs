using NonCash.Core.Interfaces;
using NonCash.Core.Services;

namespace NonCash.API.HostedServices;

/// <summary>
/// Periodically releases voucher locks older than PosService.LockTtlMinutes (Story 4-2 AC3).
/// Resets stale InUse → Pending so a fresh POS lock attempt can succeed.
/// </summary>
public class LockCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LockCleanupService> _logger;
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);

    public LockCleanupService(IServiceProvider serviceProvider, ILogger<LockCleanupService> logger)
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
                var lockRepo = scope.ServiceProvider.GetRequiredService<IVoucherLockRepository>();

                var cutoff = DateTime.UtcNow.AddMinutes(-PosService.LockTtlMinutes);
                var released = await lockRepo.ReleaseExpiredLocksAsync(cutoff, stoppingToken);

                if (released > 0)
                {
                    _logger.LogInformation("LockCleanupService released {Count} expired voucher lock(s).", released);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LockCleanupService sweep failed.");
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
}
