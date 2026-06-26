using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Infrastructure.Services;

public class ZaloPayPaymentService : IPaymentService
{
    public string GatewayName => "ZaloPay";

    private readonly HttpClient _httpClient;
    private readonly ZaloPayOptions _options;
    private readonly IRepository<PaymentTransaction> _transactionRepository;
    private readonly ILogger<ZaloPayPaymentService> _logger;

    public ZaloPayPaymentService(
        IHttpClientFactory httpClientFactory,
        IOptions<ZaloPayOptions> options,
        IRepository<PaymentTransaction> transactionRepository,
        ILogger<ZaloPayPaymentService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("ZaloPay");
        _options = options.Value;
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    public async Task<PaymentCreationResult> CreatePaymentAsync(
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_options.AppId <= 0 || string.IsNullOrWhiteSpace(_options.Key1))
        {
            return new PaymentCreationResult(
                false,
                ErrorCode: "PaymentGatewayNotConfigured",
                ErrorMessage: "ZaloPay AppId/Key1 are not configured.");
        }

        // 1. Persist a pending transaction so we have an audit record and a stable gateway id.
        var transaction = new PaymentTransaction
        {
            PurchaseOrderId = request.PurchaseOrderId,
            Gateway = GatewayName,
            GatewayTransactionId = GenerateAppTransId(),
            Amount = request.Amount,
            Currency = "VND",
            Status = PaymentStatus.Pending
        };
        await _transactionRepository.AddAsync(transaction, cancellationToken);
        await _transactionRepository.SaveChangesAsync(cancellationToken);

        var appTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var appUser = $"{_options.AppUserPrefix}_{request.MemberReference}";
        var embedData = JsonSerializer.Serialize(new
        {
            redirecturl = string.IsNullOrWhiteSpace(request.ReturnUrl)
                ? _options.RedirectUrl
                : request.ReturnUrl,
            orderId = request.PurchaseOrderId
        });
        var itemJson = "[]"; // No item breakdown needed for voucher purchase

        var macData = string.Join("|",
            _options.AppId,
            transaction.GatewayTransactionId,
            appUser,
            ((long)request.Amount).ToString(),
            appTime.ToString(),
            embedData,
            itemJson);

        var mac = ComputeHmacSha256(macData, _options.Key1);

        var form = new Dictionary<string, string>
        {
            ["app_id"] = _options.AppId.ToString(),
            ["app_trans_id"] = transaction.GatewayTransactionId,
            ["app_user"] = appUser,
            ["app_time"] = appTime.ToString(),
            ["amount"] = ((long)request.Amount).ToString(),
            ["item"] = itemJson,
            ["embed_data"] = embedData,
            ["description"] = request.Description,
            ["callback_url"] = _options.CallbackUrl,
            ["mac"] = mac
        };

        transaction.RequestPayload = JsonSerializer.Serialize(form);

        try
        {
            var response = await _httpClient.PostAsync(
                _options.Endpoint,
                new FormUrlEncodedContent(form),
                cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            transaction.ResponsePayload = body;

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var returnCode = root.GetProperty("return_code").GetInt32();
            var returnMessage = root.GetProperty("return_message").GetString();
            transaction.GatewayResponseCode = returnCode.ToString();

            if (returnCode != 1)
            {
                transaction.Status = PaymentStatus.Failed;
                transaction.CompletedAt = DateTime.UtcNow;
                _transactionRepository.Update(transaction);
                await _transactionRepository.SaveChangesAsync(cancellationToken);

                _logger.LogWarning("ZaloPay create order failed: {Message} for order {OrderId}",
                    returnMessage, request.PurchaseOrderId);

                return new PaymentCreationResult(
                    false,
                    TransactionId: transaction.Id,
                    GatewayTransactionId: transaction.GatewayTransactionId,
                    ErrorCode: $"ZaloPay_{returnCode}",
                    ErrorMessage: returnMessage);
            }

            var orderUrl = root.GetProperty("order_url").GetString();
            var zpTransToken = root.GetProperty("zp_trans_token").GetString();

            transaction.ResponsePayload = body + $"\n// zp_trans_token: {zpTransToken}";
            _transactionRepository.Update(transaction);
            await _transactionRepository.SaveChangesAsync(cancellationToken);

            return new PaymentCreationResult(
                true,
                PaymentUrl: orderUrl,
                TransactionId: transaction.Id,
                GatewayTransactionId: transaction.GatewayTransactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call ZaloPay create order for order {OrderId}",
                request.PurchaseOrderId);

            transaction.Status = PaymentStatus.Failed;
            transaction.ResponsePayload = $"{{ \"exception\": \"{ex.Message}\" }}";
            transaction.CompletedAt = DateTime.UtcNow;
            _transactionRepository.Update(transaction);
            await _transactionRepository.SaveChangesAsync(cancellationToken);

            return new PaymentCreationResult(
                false,
                TransactionId: transaction.Id,
                GatewayTransactionId: transaction.GatewayTransactionId,
                ErrorCode: "GatewayException",
                ErrorMessage: ex.Message);
        }
    }

    public Task<PaymentWebhookResult> ProcessWebhookAsync(
        string payload,
        string signature,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Key2))
        {
            return Task.FromResult(new PaymentWebhookResult(
                false,
                ErrorMessage: "ZaloPay Key2 is not configured."));
        }

        var expectedMac = ComputeHmacSha256(payload, _options.Key2);
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedMac),
            Encoding.UTF8.GetBytes(signature)))
        {
            return Task.FromResult(new PaymentWebhookResult(
                false,
                ErrorMessage: "Invalid webhook signature."));
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var appTransId = root.GetProperty("app_trans_id").GetString() ?? string.Empty;
            var zpTransId = root.GetProperty("zp_trans_id").GetString() ?? string.Empty;
            var amount = root.GetProperty("amount").GetDecimal();
            var status = root.GetProperty("status").GetInt32();

            var paymentStatus = status == 1 ? PaymentStatus.Success :
                                status == -49 ? PaymentStatus.Cancelled :
                                PaymentStatus.Failed;

            return Task.FromResult(new PaymentWebhookResult(
                true,
                MerchantTransactionId: appTransId,
                GatewayTransactionId: zpTransId,
                Status: paymentStatus,
                Amount: amount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse ZaloPay webhook payload");
            return Task.FromResult(new PaymentWebhookResult(
                false,
                ErrorMessage: $"Invalid webhook payload: {ex.Message}"));
        }
    }

    public async Task<PaymentQueryResult> QueryTransactionAsync(
        string gatewayTransactionId,
        CancellationToken cancellationToken = default)
    {
        // Not required for the first demo; stubbed for later expansion.
        await Task.CompletedTask;
        return new PaymentQueryResult(false, ErrorMessage: "Query not implemented.");
    }

    private static string GenerateAppTransId()
    {
        // ZaloPay expects a merchant reference. Use a daily prefix + short random suffix.
        var suffix = Guid.NewGuid().ToString("N")[..10];
        return $"{DateTime.UtcNow:yyMMdd}_{suffix}";
    }

    private static string ComputeHmacSha256(string data, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
