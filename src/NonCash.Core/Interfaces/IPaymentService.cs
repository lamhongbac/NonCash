using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IPaymentService
{
    string GatewayName { get; }

    Task<PaymentCreationResult> CreatePaymentAsync(
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentWebhookResult> ProcessWebhookAsync(
        string payload,
        string signature,
        CancellationToken cancellationToken = default);

    Task<PaymentQueryResult> QueryTransactionAsync(
        string gatewayTransactionId,
        CancellationToken cancellationToken = default);
}

public record CreatePaymentRequest(
    Guid PurchaseOrderId,
    decimal Amount,
    string Description,
    string MemberReference,
    string? ReturnUrl = null);

public record PaymentCreationResult(
    bool Success,
    string? PaymentUrl = null,
    Guid? TransactionId = null,
    string? GatewayTransactionId = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record PaymentWebhookResult(
    bool Success,
    string? MerchantTransactionId = null,
    string? GatewayTransactionId = null,
    PaymentStatus? Status = null,
    decimal? Amount = null,
    string? ErrorMessage = null);

public record PaymentQueryResult(
    bool Success,
    PaymentStatus? Status = null,
    string? GatewayTransactionId = null,
    string? ErrorMessage = null);
