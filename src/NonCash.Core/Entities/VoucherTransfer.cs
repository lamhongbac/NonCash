namespace NonCash.Core.Entities;

public enum VoucherTransferStatus
{
    PendingAcceptance,
    Accepted,
    Rejected,
    Expired,
    Cancelled
}

public enum VoucherTransferType
{
    Gift
}

public class VoucherTransfer : BaseEntity
{
    public Guid SenderId { get; set; }
    public Guid RecipientId { get; set; }
    public Guid VoucherId { get; set; }
    public VoucherTransferStatus Status { get; set; } = VoucherTransferStatus.PendingAcceptance;
    public VoucherTransferType TransferType { get; set; } = VoucherTransferType.Gift;
    public DateTime InitiatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? Note { get; set; }
    public string? RejectReason { get; set; }
    public DateTime? RespondedAt { get; set; }

    // Navigation properties - point to UserAccount (JWT subject) not Customer
    public UserAccount? Sender { get; set; }
    public UserAccount? Recipient { get; set; }
    public VoucherPlanDetail? Voucher { get; set; }
}
