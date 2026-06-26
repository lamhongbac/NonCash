namespace NonCash.Core.Entities;

public enum OrderStatus
{
    PendingPayment,
    Paid,
    Cancelled
}

public class PurchaseOrder : BaseEntity
{
    public Guid MemberId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.PendingPayment;
    public string? InvoiceCompanyName { get; set; }
    public string? InvoiceTaxCode { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime? PaidAt { get; set; }

    // Navigation properties
    public MemberAccount? Member { get; set; }
    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}

public class OrderDetail : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid PlanId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    // Navigation properties
    public PurchaseOrder? Order { get; set; }
    public VoucherPlanHeader? Plan { get; set; }
}
