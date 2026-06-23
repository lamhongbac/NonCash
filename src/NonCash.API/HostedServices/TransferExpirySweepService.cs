using NonCash.Core.Interfaces;

namespace NonCash.API.HostedServices;

/// <summary>
/// Periodically expires pending peer-to-peer voucher transfers past their ExpiresAt (Story 5-2 AC6).
/// Releases the associated voucher soft-locks so the sender can use or re-transfer the voucher.
/// </summary>
public class TransferExpirySweepService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TransferExpirySweepService> _logger;
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);

    public TransferExpirySweepService(IServiceProvider serviceProvider, ILogger<TransferExpirySweepService> logger)
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
                var transferRepo = scope.ServiceProvider.GetRequiredService<IVoucherTransferRepository>();

                var now = DateTime.UtcNow;
                var expired = await transferRepo.SweepExpiredAsync(now, stoppingToken);

                if (expired > 0)
                {
                    _logger.LogInformation("TransferExpirySweepService expired {Count} pending transfer(s).", expired);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TransferExpirySweepService sweep failed.");
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
