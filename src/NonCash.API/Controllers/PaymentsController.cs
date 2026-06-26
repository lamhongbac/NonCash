using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;
using NonCash.Infrastructure.Services;
using System.Text.Json;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/payments")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IPurchaseService _purchaseService;
    private readonly ICurrentUserService _currentUser;
    private readonly IRepository<PurchaseOrder> _orderRepository;
    private readonly IRepository<PaymentTransaction> _transactionRepository;
    private readonly ApplicationDbContext _context;
    private readonly ZaloPayOptions _zaloPayOptions;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IPaymentService paymentService,
        IPurchaseService purchaseService,
        ICurrentUserService currentUser,
        IRepository<PurchaseOrder> orderRepository,
        IRepository<PaymentTransaction> transactionRepository,
        ApplicationDbContext context,
        IOptions<ZaloPayOptions> zaloPayOptions,
        ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _purchaseService = purchaseService;
        _currentUser = currentUser;
        _orderRepository = orderRepository;
        _transactionRepository = transactionRepository;
        _context = context;
        _zaloPayOptions = zaloPayOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Creates a ZaloPay payment session for an existing pending order.
    /// </summary>
    [HttpPost("{orderId:guid}/create")]
    public async Task<ActionResult<PaymentCreateResponse>> CreatePayment(
        Guid orderId,
        [FromBody] PaymentCreateRequest? request,
        CancellationToken cancellationToken)
    {
        var memberIdValue = _currentUser.GetCurrentUserId();
        if (!Guid.TryParse(memberIdValue, out var memberId))
            return Unauthorized(new { error = "Unauthorized", message = "Member identity is required." });

        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order == null)
            return NotFound(new { error = "OrderNotFound", message = "Order not found." });

        if (order.MemberId != memberId)
            return Forbid();

        if (order.Status != OrderStatus.PendingPayment)
            return BadRequest(new { error = "InvalidOrderState", message = $"Order is {order.Status}." });

        var memberReference = memberIdValue ?? memberId.ToString();
        var createRequest = new CreatePaymentRequest(
            order.Id,
            order.TotalAmount,
            $"NonCash voucher order #{order.Id:N}",
            memberReference,
            request?.ReturnUrl ?? _zaloPayOptions.RedirectUrl);

        var result = await _paymentService.CreatePaymentAsync(createRequest, cancellationToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.PaymentUrl))
        {
            return StatusCode(502, new
            {
                error = result.ErrorCode ?? "PaymentGatewayError",
                message = result.ErrorMessage ?? "Unable to create payment session."
            });
        }

        return Ok(new PaymentCreateResponse(
            result.PaymentUrl,
            result.TransactionId.GetValueOrDefault(),
            result.GatewayTransactionId!));
    }

    /// <summary>
    /// ZaloPay return URL. Only used for UX redirection; real status comes from the webhook.
    /// </summary>
    [HttpGet("callback")]
    [AllowAnonymous]
    public IActionResult Callback(
        [FromQuery] string? status,
        [FromQuery(Name = "apptransid")] string? appTransId)
    {
        var redirect = $"{_zaloPayOptions.RedirectUrl}?appTransId={Uri.EscapeDataString(appTransId ?? string.Empty)}&status={Uri.EscapeDataString(status ?? string.Empty)}";
        return Redirect(redirect);
    }

    /// <summary>
    /// ZaloPay server-side webhook. Verified, idempotent, and triggers order fulfillment.
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(
        [FromBody] ZaloPayWebhookPayload payload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.Data) || string.IsNullOrWhiteSpace(payload.Mac))
        {
            return Ok(new { return_code = -1, return_message = "Missing data or mac" });
        }

        var result = await _paymentService.ProcessWebhookAsync(payload.Data, payload.Mac, cancellationToken);
        if (!result.Success)
        {
            _logger.LogWarning("ZaloPay webhook rejected: {Message}", result.ErrorMessage);
            return Ok(new { return_code = -1, return_message = result.ErrorMessage });
        }

        var transaction = await _transactionRepository.FindAsync(
            t => t.GatewayTransactionId == result.MerchantTransactionId,
            cancellationToken);

        var tx = transaction.FirstOrDefault();
        if (tx == null)
        {
            _logger.LogWarning("ZaloPay webhook references unknown transaction {AppTransId}", result.MerchantTransactionId);
            return Ok(new { return_code = -1, return_message = "Transaction not found" });
        }

        tx.WebhookPayload = payload.Data;
        tx.Status = result.Status ?? PaymentStatus.Failed;
        tx.CompletedAt = DateTime.UtcNow;
        _transactionRepository.Update(tx);
        await _transactionRepository.SaveChangesAsync(cancellationToken);

        if (tx.Status == PaymentStatus.Success)
        {
            var order = await _orderRepository.GetByIdAsync(tx.PurchaseOrderId, cancellationToken);
            if (order != null && order.Status == OrderStatus.PendingPayment)
            {
                var confirmResult = await _purchaseService.ConfirmPaymentAsync(order.Id, cancellationToken);
                if (!confirmResult.Success)
                {
                    _logger.LogError(
                        "Payment succeeded but order fulfillment failed for order {OrderId}: {Message}",
                        order.Id, confirmResult.ErrorMessage);
                    // Still acknowledge the webhook; fulfillment must be retried separately.
                }
            }
        }

        return Ok(new { return_code = 1, return_message = "success" });
    }

    /// <summary>
    /// Poll endpoint for the Blazor client to refresh order status after returning from ZaloPay.
    /// </summary>
    [HttpGet("transactions/{transactionId:guid}")]
    public async Task<ActionResult<PaymentTransactionResponse>> GetTransaction(
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var tx = await _transactionRepository.GetByIdAsync(transactionId, cancellationToken);
        if (tx == null)
            return NotFound();

        if (!Guid.TryParse(_currentUser.GetCurrentUserId(), out var memberId))
            return Unauthorized();

        var order = await _orderRepository.GetByIdAsync(tx.PurchaseOrderId, cancellationToken);
        if (order == null || order.MemberId != memberId)
            return Forbid();

        return Ok(new PaymentTransactionResponse(
            tx.Id,
            tx.PurchaseOrderId,
            tx.Status.ToString(),
            tx.Amount,
            tx.GatewayTransactionId));
    }

    /// <summary>
    /// Looks up a transaction by the gateway's own transaction id (e.g. ZaloPay apptransid).
    /// Used by the return-URL status page.
    /// </summary>
    [HttpGet("transactions/by-gateway/{gatewayTransactionId}")]
    public async Task<ActionResult<PaymentTransactionResponse>> GetByGatewayTransactionId(
        string gatewayTransactionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(gatewayTransactionId))
            return BadRequest();

        var tx = await _context.PaymentTransactions
            .AsNoTracking()
            .Include(t => t.PurchaseOrder)
            .FirstOrDefaultAsync(
                t => t.GatewayTransactionId == gatewayTransactionId,
                cancellationToken);

        if (tx?.PurchaseOrder == null)
            return NotFound();

        if (!Guid.TryParse(_currentUser.GetCurrentUserId(), out var memberId))
            return Unauthorized();

        if (tx.PurchaseOrder.MemberId != memberId)
            return Forbid();

        return Ok(new PaymentTransactionResponse(
            tx.Id,
            tx.PurchaseOrderId,
            tx.Status.ToString(),
            tx.Amount,
            tx.GatewayTransactionId));
    }
}

public record PaymentCreateRequest(string? ReturnUrl);

public record PaymentCreateResponse(
    string PaymentUrl,
    Guid TransactionId,
    string GatewayTransactionId);

public record ZaloPayWebhookPayload(string Data, string Mac);

public record PaymentTransactionResponse(
    Guid Id,
    Guid PurchaseOrderId,
    string Status,
    decimal Amount,
    string GatewayTransactionId);
