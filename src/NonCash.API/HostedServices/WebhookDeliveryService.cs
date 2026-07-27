using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NonCash.Core.Entities;
using NonCash.Infrastructure.Data;

namespace NonCash.API.HostedServices;

/// <summary>
/// Background service that reads unprocessed VoucherEvents and delivers them
/// to active IntegrationPartners via HTTP POST with HMAC-SHA256 signature.
/// Implements retry with exponential backoff (1m, 5m, 25m, 2h, 10h — max 5 retries).
/// </summary>
public class WebhookDeliveryService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly int MaxRetries = 5;
    private static readonly int[] BackoffMinutes = { 1, 5, 25, 120, 600 };

    private readonly IServiceProvider _services;
    private readonly ILogger<WebhookDeliveryService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public WebhookDeliveryService(
        IServiceProvider services,
        ILogger<WebhookDeliveryService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _services = services;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WebhookDeliveryService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingDeliveries(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in WebhookDeliveryService loop.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessPendingDeliveries(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTime.UtcNow;
        var pending = await context.Set<WebhookDelivery>()
            .Include(d => d.Partner)
            .Include(d => d.Event)
            .Where(d => d.DeliveredAt == null
                     && d.RetryCount < MaxRetries
                     && d.NextRetryAt <= now)
            .OrderBy(d => d.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var delivery in pending)
        {
            await DeliverWebhook(context, delivery, cancellationToken);
        }
    }

    private async Task DeliverWebhook(
        ApplicationDbContext context,
        WebhookDelivery delivery,
        CancellationToken cancellationToken)
    {
        var partner = delivery.Partner;
        var evt = delivery.Event;

        if (partner == null || evt == null)
        {
            delivery.LastError = "Missing partner or event data.";
            delivery.RetryCount++;
            ScheduleRetry(delivery);
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("WebhookDelivery");
            var payload = JsonSerializer.Serialize(new
            {
                @event = evt.EventType,
                voucherId = evt.VoucherId,
                memberPhone = evt.MemberPhone,
                brandId = evt.BrandId,
                data = JsonSerializer.Deserialize<object>(evt.PayloadJson),
                timestamp = evt.CreatedAt
            });

            var request = new HttpRequestMessage(HttpMethod.Post, partner.CallbackUrl)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            // HMAC-SHA256 signature
            var signature = ComputeHmac(payload, partner.WebhookSecret);
            request.Headers.Add("X-NonCash-Signature", $"sha256={signature}");
            request.Headers.Add("X-NonCash-Event", evt.EventType);

            var response = await client.SendAsync(request, cancellationToken);
            delivery.HttpStatus = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                delivery.DeliveredAt = DateTime.UtcNow;
                delivery.LastError = null;
                _logger.LogInformation("Webhook delivered to {Partner} for event {Event}", partner.Name, evt.EventType);
            }
            else
            {
                delivery.LastError = $"HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}";
                delivery.RetryCount++;
                ScheduleRetry(delivery);
                _logger.LogWarning("Webhook delivery failed to {Partner}: {Error}", partner.Name, delivery.LastError);
            }
        }
        catch (Exception ex)
        {
            delivery.LastError = ex.Message;
            delivery.RetryCount++;
            ScheduleRetry(delivery);
            _logger.LogError(ex, "Webhook delivery exception for {Partner}", partner.Name);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static void ScheduleRetry(WebhookDelivery delivery)
    {
        if (delivery.RetryCount >= MaxRetries)
        {
            delivery.NextRetryAt = null; // Give up
            return;
        }

        var minutes = delivery.RetryCount < BackoffMinutes.Length
            ? BackoffMinutes[delivery.RetryCount]
            : BackoffMinutes[^1];

        delivery.NextRetryAt = DateTime.UtcNow.AddMinutes(minutes);
    }

    private static string ComputeHmac(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(keyBytes, payloadBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
