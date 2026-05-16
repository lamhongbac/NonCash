using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.Core.Interfaces;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IPurchaseService _purchaseService;
    private readonly ICurrentUserService _currentUser;

    // Payment confirmation is restricted to internal/admin roles for MVP (NFR3)
    private static readonly string[] PaymentConfirmRoles = { "Admin", "BrandManager" };

    public OrdersController(IPurchaseService purchaseService, ICurrentUserService currentUser)
    {
        _purchaseService = purchaseService;
        _currentUser = currentUser;
    }

    // AC2 + AC5: Create purchase order with optional invoice info
    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.PlanId == Guid.Empty || request.MemberId == Guid.Empty)
            return BadRequest(new { error = "Validation", message = "PlanId and MemberId are required." });

        var input = new CreateOrderInput(
            request.MemberId,
            request.PlanId,
            request.Quantity,
            request.Invoice?.CompanyName,
            request.Invoice?.TaxCode);

        var result = await _purchaseService.CreateOrderAsync(input, cancellationToken);
        return MapResult(result, asCreated: true);
    }

    // AC3: Payment confirmation (Admin/internal endpoint for MVP)
    [HttpPost("{orderId:guid}/pay")]
    public async Task<ActionResult<OrderResponse>> Pay(Guid orderId, CancellationToken cancellationToken)
    {
        var role = _currentUser.GetCurrentUserRole();
        if (string.IsNullOrEmpty(role) || !PaymentConfirmRoles.Contains(role))
            return Forbid();

        var result = await _purchaseService.ConfirmPaymentAsync(orderId, cancellationToken);
        return MapResult(result);
    }

    [HttpPost("{orderId:guid}/cancel")]
    public async Task<ActionResult<OrderResponse>> Cancel(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await _purchaseService.CancelOrderAsync(orderId, cancellationToken);
        return MapResult(result);
    }

    [HttpGet("{orderId:guid}")]
    public async Task<ActionResult<OrderResponse>> Get(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _purchaseService.GetByIdAsync(orderId, cancellationToken);
        if (order == null)
            return NotFound(new { error = "OrderNotFound" });

        return Ok(MapToResponse(order));
    }

    private ActionResult<OrderResponse> MapResult(OrderResult result, bool asCreated = false)
    {
        if (result.Success && result.Order != null)
        {
            var response = MapToResponse(result.Order);
            return asCreated
                ? CreatedAtAction(nameof(Get), new { orderId = result.Order.Id }, response)
                : Ok(response);
        }

        return result.ErrorCode switch
        {
            "OrderNotFound" or "PlanNotFound" or "MemberNotFound" => NotFound(new { error = result.ErrorCode, message = result.ErrorMessage }),
            "InsufficientStock" => StatusCode(400, new { error = result.ErrorCode, message = result.ErrorMessage }),
            _ => BadRequest(new { error = result.ErrorCode ?? "Validation", message = result.ErrorMessage })
        };
    }

    private static OrderResponse MapToResponse(NonCash.Core.Entities.PurchaseOrder o) => new(
        o.Id,
        o.MemberId,
        o.Status.ToString(),
        o.TotalAmount,
        o.PaidAt,
        o.InvoiceCompanyName,
        o.InvoiceTaxCode,
        o.OrderDetails.Select(d => new OrderLineResponse(d.PlanId, d.Quantity, d.UnitPrice)).ToList(),
        o.CreatedAt
    );
}

public record CreateOrderRequest(Guid MemberId, Guid PlanId, int Quantity, InvoiceInfo? Invoice);

public record InvoiceInfo(string CompanyName, string TaxCode);

public record OrderResponse(
    Guid Id,
    Guid MemberId,
    string Status,
    decimal TotalAmount,
    DateTime? PaidAt,
    string? InvoiceCompanyName,
    string? InvoiceTaxCode,
    List<OrderLineResponse> Items,
    DateTime CreatedAt);

public record OrderLineResponse(Guid PlanId, int Quantity, decimal UnitPrice);
