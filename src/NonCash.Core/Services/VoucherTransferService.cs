using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public class VoucherTransferService : IVoucherTransferService
{
    public const int TransferExpiryDays = 7;

    private readonly IRepository<VoucherPlanDetail> _detailRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IMemberAccountRepository _memberRepository;
    private readonly IVoucherTransferRepository _transferRepository;

    public VoucherTransferService(
        IRepository<VoucherPlanDetail> detailRepository,
        ICustomerRepository customerRepository,
        IMemberAccountRepository memberRepository,
        IVoucherTransferRepository transferRepository)
    {
        _detailRepository = detailRepository;
        _customerRepository = customerRepository;
        _memberRepository = memberRepository;
        _transferRepository = transferRepository;
    }

    public async Task<InitiateTransferResult> InitiateAsync(
        Guid senderId,
        Guid voucherId,
        string? recipientPhone,
        string? recipientMemberId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var voucher = await _detailRepository.GetByIdAsync(voucherId, cancellationToken);
        if (voucher == null)
            return new InitiateTransferResult(false, ErrorCode: "VoucherNotFound", ErrorMessage: "Voucher not found.");

        if (voucher.MemberId != senderId)
            return new InitiateTransferResult(false, ErrorCode: "NotOwned", ErrorMessage: "Voucher is not owned by you.");

        if (voucher.UsageStatus != UsageStatus.Pending)
            return new InitiateTransferResult(false, ErrorCode: "NotTransferable", ErrorMessage: "Voucher is not in Pending status.");

        var existingPending = await _transferRepository.FindPendingByVoucherAsync(voucherId, cancellationToken);
        if (existingPending != null)
            return new InitiateTransferResult(false, ErrorCode: "TransferAlreadyPending", ErrorMessage: "A transfer for this voucher is already pending.");

        var recipient = await ResolveRecipientAsync(recipientPhone, recipientMemberId, cancellationToken);
        if (recipient == null)
            return new InitiateTransferResult(false, ErrorCode: "RecipientNotFound", ErrorMessage: "Recipient could not be resolved.");

        if (recipient.Id == senderId)
            return new InitiateTransferResult(false, ErrorCode: "SelfTransferNotAllowed", ErrorMessage: "Cannot transfer a voucher to yourself.");

        var now = DateTime.UtcNow;
        var transfer = new VoucherTransfer
        {
            SenderId = senderId,
            RecipientId = recipient.Id,
            VoucherId = voucherId,
            Status = VoucherTransferStatus.PendingAcceptance,
            TransferType = VoucherTransferType.Gift,
            InitiatedAt = now,
            ExpiresAt = now.AddDays(TransferExpiryDays),
            Note = note
        };

        await _transferRepository.AddAsync(transfer, cancellationToken);

        voucher.TransferLockId = transfer.Id;
        voucher.TransferLockedAt = now;
        _detailRepository.Update(voucher);
        await _transferRepository.SaveChangesAsync(cancellationToken);

        return new InitiateTransferResult(true, TransferId: transfer.Id);
    }

    public async Task<TransferActionResult> AcceptAsync(
        Guid transferId,
        Guid recipientId,
        CancellationToken cancellationToken = default)
    {
        var transfer = await _transferRepository.GetByIdAsync(transferId, cancellationToken);
        if (transfer == null)
            return new TransferActionResult(false, ErrorCode: "TransferNotFound", ErrorMessage: "Transfer not found.");

        if (transfer.RecipientId != recipientId)
            return new TransferActionResult(false, ErrorCode: "Forbidden", ErrorMessage: "Only the recipient can accept this transfer.");

        var expired = await _transferRepository.EnsureNotExpiredAsync(transferId, cancellationToken);
        if (expired != null)
            return expired;

        if (transfer.Status != VoucherTransferStatus.PendingAcceptance)
            return new TransferActionResult(false, Status: transfer.Status.ToString(), ErrorCode: "AlreadyResolved", ErrorMessage: $"Transfer is already {transfer.Status}.");

        return await _transferRepository.AcceptAsync(transferId, recipientId, cancellationToken);
    }

    public async Task<TransferActionResult> RejectAsync(
        Guid transferId,
        Guid recipientId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var transfer = await _transferRepository.GetByIdAsync(transferId, cancellationToken);
        if (transfer == null)
            return new TransferActionResult(false, ErrorCode: "TransferNotFound", ErrorMessage: "Transfer not found.");

        if (transfer.RecipientId != recipientId)
            return new TransferActionResult(false, ErrorCode: "Forbidden", ErrorMessage: "Only the recipient can reject this transfer.");

        var expired = await _transferRepository.EnsureNotExpiredAsync(transferId, cancellationToken);
        if (expired != null)
            return expired;

        if (transfer.Status != VoucherTransferStatus.PendingAcceptance)
            return new TransferActionResult(false, Status: transfer.Status.ToString(), ErrorCode: "AlreadyResolved", ErrorMessage: $"Transfer is already {transfer.Status}.");

        return await _transferRepository.RejectAsync(transferId, recipientId, reason, cancellationToken);
    }

    public async Task<TransferActionResult> CancelAsync(
        Guid transferId,
        Guid senderId,
        CancellationToken cancellationToken = default)
    {
        var transfer = await _transferRepository.GetByIdAsync(transferId, cancellationToken);
        if (transfer == null)
            return new TransferActionResult(false, ErrorCode: "TransferNotFound", ErrorMessage: "Transfer not found.");

        if (transfer.SenderId != senderId)
            return new TransferActionResult(false, ErrorCode: "Forbidden", ErrorMessage: "Only the sender can cancel this transfer.");

        var expired = await _transferRepository.EnsureNotExpiredAsync(transferId, cancellationToken);
        if (expired != null)
            return expired;

        if (transfer.Status != VoucherTransferStatus.PendingAcceptance)
            return new TransferActionResult(false, Status: transfer.Status.ToString(), ErrorCode: "AlreadyResolved", ErrorMessage: $"Transfer is already {transfer.Status}.");

        return await _transferRepository.CancelAsync(transferId, senderId, cancellationToken);
    }

    public Task<IReadOnlyList<TransferInboxItem>> GetInboxAsync(
        Guid recipientId,
        VoucherTransferStatus? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return _transferRepository.GetInboxAsync(recipientId, status, page, pageSize, cancellationToken);
    }

    public Task<IReadOnlyList<TransferOutboxItem>> GetOutboxAsync(
        Guid senderId,
        VoucherTransferStatus? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return _transferRepository.GetOutboxAsync(senderId, status, page, pageSize, cancellationToken);
    }

    /// <summary>
    /// Resolves the recipient to a MemberAccount. The transfer feature uses
    /// MemberAccount.Id for both SenderId and RecipientId (JWT subject).
    /// If the recipient is found by phone but has no member account, a placeholder
    /// member account is created and linked to the customer profile.
    /// </summary>
    private async Task<MemberAccount?> ResolveRecipientAsync(
        string? recipientPhone,
        string? recipientMemberId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(recipientPhone))
        {
            var normalized = Customer.NormalizePhoneNumber(recipientPhone);
            if (string.IsNullOrEmpty(normalized))
                return null;

            var customer = await _customerRepository.GetByPhoneNumberAsync(normalized, cancellationToken);
            if (customer == null)
            {
                // Create placeholder customer + member account
                var placeholderCustomer = new Customer
                {
                    PhoneNumber = normalized,
                    FullName = normalized,
                    Status = CustomerStatus.Active
                };
                await _customerRepository.AddAsync(placeholderCustomer, cancellationToken);
                await _customerRepository.SaveChangesAsync(cancellationToken);

                var newMember = new MemberAccount
                {
                    Username = normalized,
                    PasswordHash = string.Empty, // Not usable for login until registered
                    FullName = normalized,
                    CustomerId = placeholderCustomer.Id,
                    Status = MemberAccountStatus.Active
                };
                return await _memberRepository.AddAsync(newMember, cancellationToken);
            }

            var existingMember = await _memberRepository.GetByCustomerIdAsync(customer.Id, cancellationToken);
            if (existingMember != null)
                return existingMember;

            // Create placeholder member account linked to existing customer
            var placeholderMember = new MemberAccount
            {
                Username = normalized,
                PasswordHash = string.Empty, // Not usable for login until registered
                FullName = customer.FullName,
                CustomerId = customer.Id,
                Status = MemberAccountStatus.Active
            };
            return await _memberRepository.AddAsync(placeholderMember, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(recipientMemberId)
            && Guid.TryParse(recipientMemberId, out var memberId))
        {
            return await _memberRepository.GetByIdAsync(memberId, cancellationToken);
        }

        return null;
    }
}
