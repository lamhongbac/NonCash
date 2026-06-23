using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public class VoucherTransferService : IVoucherTransferService
{
    public const int TransferExpiryDays = 7;

    private readonly IRepository<VoucherPlanDetail> _detailRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IVoucherTransferRepository _transferRepository;

    public VoucherTransferService(
        IRepository<VoucherPlanDetail> detailRepository,
        ICustomerRepository customerRepository,
        IUserAccountRepository userAccountRepository,
        IVoucherTransferRepository transferRepository)
    {
        _detailRepository = detailRepository;
        _customerRepository = customerRepository;
        _userAccountRepository = userAccountRepository;
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
    /// Resolves the recipient to a UserAccount. The transfer feature uses
    /// UserAccount.Id for both SenderId and RecipientId (JWT subject).
    /// If the recipient is found by phone but has no user account, a placeholder
    /// user account is created and linked to the customer profile.
    /// </summary>
    private async Task<UserAccount?> ResolveRecipientAsync(
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
            if (customer != null)
            {
                var existingUser = await _userAccountRepository.GetByCustomerIdAsync(customer.Id, cancellationToken);
                if (existingUser != null)
                    return existingUser;

                // Create placeholder user account linked to existing customer
                var placeholderUser = new UserAccount
                {
                    Username = normalized,
                    PasswordHash = string.Empty, // Not usable for login until registered
                    FullName = customer.FullName,
                    CustomerId = customer.Id,
                    Role = UserRole.BrandManager,
                    Status = UserStatus.Active
                };
                return await _userAccountRepository.AddAsync(placeholderUser, cancellationToken);
            }

            // No customer yet - create placeholder customer + user account
            var placeholderCustomer = new Customer
            {
                PhoneNumber = normalized,
                FullName = normalized,
                Status = CustomerStatus.Active
            };
            await _customerRepository.AddAsync(placeholderCustomer, cancellationToken);

            var newUser = new UserAccount
            {
                Username = normalized,
                PasswordHash = string.Empty,
                FullName = normalized,
                CustomerId = placeholderCustomer.Id,
                Role = UserRole.BrandManager,
                Status = UserStatus.Active
            };
            return await _userAccountRepository.AddAsync(newUser, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(recipientMemberId)
            && Guid.TryParse(recipientMemberId, out var memberId))
        {
            return await _userAccountRepository.GetByIdAsync(memberId, cancellationToken);
        }

        return null;
    }
}
