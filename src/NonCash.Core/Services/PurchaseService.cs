using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public class PurchaseService : IPurchaseService
{
    private readonly IVoucherPlanRepository _planRepository;
    private readonly IRepository<VoucherPlanDetail> _detailRepository;
    private readonly IRepository<PurchaseOrder> _orderRepository;
    private readonly IRepository<OrderDetail> _orderDetailRepository;
    private readonly IRepository<VoucherDistribution> _distributionRepository;
    private readonly ICustomerRepository _customerRepository;

    public PurchaseService(
        IVoucherPlanRepository planRepository,
        IRepository<VoucherPlanDetail> detailRepository,
        IRepository<PurchaseOrder> orderRepository,
        IRepository<OrderDetail> orderDetailRepository,
        IRepository<VoucherDistribution> distributionRepository,
        ICustomerRepository customerRepository)
    {
        _planRepository = planRepository;
        _detailRepository = detailRepository;
        _orderRepository = orderRepository;
        _orderDetailRepository = orderDetailRepository;
        _distributionRepository = distributionRepository;
        _customerRepository = customerRepository;
    }

    public async Task<IReadOnlyList<VoucherPlanHeader>> ListCatalogAsync(CancellationToken cancellationToken = default)
    {
        // AC1: Approved gift vouchers, currently within validity window
        var now = DateTime.UtcNow;
        var plans = await _planRepository.FindAsync(
            p => p.VoucherType == VoucherType.Gift
              && p.ApprovalStatus == ApprovalStatus.Approved
              && p.PublishDate <= now
              && p.ExpiryDate > now,
            cancellationToken);

        return plans.OrderByDescending(p => p.PublishDate).ToList();
    }

    public async Task<OrderResult> CreateOrderAsync(CreateOrderInput input, CancellationToken cancellationToken = default)
    {
        if (input.Quantity <= 0)
            return new OrderResult(false, ErrorCode: "InvalidQuantity", ErrorMessage: "Quantity must be greater than 0.");

        // Validate member
        var member = await _customerRepository.GetByIdAsync(input.MemberId, cancellationToken);
        if (member == null)
            return new OrderResult(false, ErrorCode: "MemberNotFound", ErrorMessage: "Member not found.");
        if (member.Status == CustomerStatus.Blacklisted)
            return new OrderResult(false, ErrorCode: "MemberBlacklisted", ErrorMessage: "Blacklisted members cannot purchase.");

        // Validate plan eligibility
        var plan = await _planRepository.GetByIdAsync(input.PlanId, cancellationToken);
        if (plan == null)
            return new OrderResult(false, ErrorCode: "PlanNotFound", ErrorMessage: "Voucher plan not found.");
        if (plan.VoucherType != VoucherType.Gift)
            return new OrderResult(false, ErrorCode: "PlanNotPurchasable", ErrorMessage: "Only gift vouchers can be purchased.");
        if (plan.ApprovalStatus != ApprovalStatus.Approved)
            return new OrderResult(false, ErrorCode: "PlanNotApproved", ErrorMessage: "Plan is not available for sale.");
        if (plan.ExpiryDate <= DateTime.UtcNow)
            return new OrderResult(false, ErrorCode: "PlanExpired", ErrorMessage: "Plan has expired.");

        // AC6: Check stock at order creation (advisory; final check happens at payment)
        var available = (await _detailRepository.FindAsync(
            d => d.ParentId == plan.Id && d.MemberId == null && d.UsageStatus == UsageStatus.Pending,
            cancellationToken)).Count();

        if (available < input.Quantity)
            return new OrderResult(false, ErrorCode: "InsufficientStock", ErrorMessage: $"Only {available} vouchers available.");

        var unitPrice = plan.NetValue;
        var order = new PurchaseOrder
        {
            MemberId = input.MemberId,
            Status = OrderStatus.PendingPayment,
            InvoiceCompanyName = input.InvoiceCompanyName?.Trim(),
            InvoiceTaxCode = input.InvoiceTaxCode?.Trim(),
            TotalAmount = unitPrice * input.Quantity
        };
        await _orderRepository.AddAsync(order, cancellationToken);

        var detail = new OrderDetail
        {
            OrderId = order.Id,
            PlanId = plan.Id,
            Quantity = input.Quantity,
            UnitPrice = unitPrice
        };
        await _orderDetailRepository.AddAsync(detail, cancellationToken);

        await _orderRepository.SaveChangesAsync(cancellationToken);

        order.OrderDetails = new List<OrderDetail> { detail };
        return new OrderResult(true, Order: order);
    }

    public async Task<OrderResult> ConfirmPaymentAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order == null)
            return new OrderResult(false, ErrorCode: "OrderNotFound", ErrorMessage: "Order not found.");
        if (order.Status == OrderStatus.Paid)
            return new OrderResult(true, Order: order); // Idempotent
        if (order.Status == OrderStatus.Cancelled)
            return new OrderResult(false, ErrorCode: "OrderCancelled", ErrorMessage: "Cancelled orders cannot be paid.");

        // Load order details
        var orderDetails = (await _orderDetailRepository.FindAsync(d => d.OrderId == orderId, cancellationToken)).ToList();
        if (orderDetails.Count == 0)
            return new OrderResult(false, ErrorCode: "EmptyOrder", ErrorMessage: "Order has no items.");

        var totalAllocated = 0;
        var now = DateTime.UtcNow;

        foreach (var od in orderDetails)
        {
            // AC4: Allocate Q vouchers per line item
            var available = (await _detailRepository.FindAsync(
                d => d.ParentId == od.PlanId && d.MemberId == null && d.UsageStatus == UsageStatus.Pending,
                cancellationToken)).OrderBy(d => d.SerialNo).Take(od.Quantity).ToList();

            if (available.Count < od.Quantity)
                return new OrderResult(false, ErrorCode: "InsufficientStock",
                    ErrorMessage: $"Plan {od.PlanId}: required {od.Quantity}, available {available.Count}.");

            for (var i = 0; i < od.Quantity; i++)
            {
                var tracked = await _detailRepository.GetByIdAsync(available[i].Id, cancellationToken);
                if (tracked == null || tracked.MemberId != null)
                    return new OrderResult(false, ErrorCode: "ConcurrencyConflict",
                        ErrorMessage: "Voucher stock changed during allocation. Please retry.");

                tracked.MemberId = order.MemberId;
                _detailRepository.Update(tracked);

                await _distributionRepository.AddAsync(new VoucherDistribution
                {
                    VoucherId = tracked.Id,
                    MemberId = order.MemberId,
                    Method = DistributionMethod.Sale,
                    DistributionDate = now
                }, cancellationToken);

                totalAllocated++;
            }

            // Update plan target distributed
            var plan = await _planRepository.GetByIdAsync(od.PlanId, cancellationToken);
            if (plan != null)
            {
                plan.TargetDistributed += od.Quantity;
                _planRepository.Update(plan);
            }
        }

        order.Status = OrderStatus.Paid;
        order.PaidAt = now;
        _orderRepository.Update(order);

        await _orderRepository.SaveChangesAsync(cancellationToken);

        return new OrderResult(true, Order: order, AllocatedCount: totalAllocated);
    }

    public async Task<OrderResult> CancelOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order == null)
            return new OrderResult(false, ErrorCode: "OrderNotFound", ErrorMessage: "Order not found.");
        if (order.Status == OrderStatus.Paid)
            return new OrderResult(false, ErrorCode: "OrderAlreadyPaid", ErrorMessage: "Paid orders cannot be cancelled here.");
        if (order.Status == OrderStatus.Cancelled)
            return new OrderResult(true, Order: order); // Idempotent

        order.Status = OrderStatus.Cancelled;
        _orderRepository.Update(order);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        return new OrderResult(true, Order: order);
    }

    public async Task<PurchaseOrder?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order == null) return null;

        var details = await _orderDetailRepository.FindAsync(d => d.OrderId == orderId, cancellationToken);
        order.OrderDetails = details.ToList();
        return order;
    }
}
