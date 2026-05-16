using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface IPurchaseService
{
    Task<OrderResult> CreateOrderAsync(CreateOrderInput input, CancellationToken cancellationToken = default);
    Task<OrderResult> ConfirmPaymentAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<OrderResult> CancelOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<PurchaseOrder?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VoucherPlanHeader>> ListCatalogAsync(CancellationToken cancellationToken = default);
}

public record CreateOrderInput(
    Guid MemberId,
    Guid PlanId,
    int Quantity,
    string? InvoiceCompanyName,
    string? InvoiceTaxCode);

public record OrderResult(
    bool Success,
    PurchaseOrder? Order = null,
    int AllocatedCount = 0,
    string? ErrorCode = null,
    string? ErrorMessage = null);
