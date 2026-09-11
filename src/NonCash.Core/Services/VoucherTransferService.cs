using Microsoft.Extensions.Configuration;
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
    private readonly IBrandCustomerRepository _brandCustomerRepository;
    private readonly IVoucherPlanRepository _planRepository;
    private readonly IBrandRepository _brandRepository;
    private readonly IRepository<VoucherDistribution> _distributionRepository;
    private readonly INotificationService _notificationService;
    private readonly IVoucherEventPublisher _eventPublisher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IConfiguration _configuration;

    public VoucherTransferService(
        IRepository<VoucherPlanDetail> detailRepository,
        ICustomerRepository customerRepository,
        IMemberAccountRepository memberRepository,
        IVoucherTransferRepository transferRepository,
        IBrandCustomerRepository brandCustomerRepository,
        IVoucherPlanRepository planRepository,
        IBrandRepository brandRepository,
        IRepository<VoucherDistribution> distributionRepository,
        INotificationService notificationService,
        IVoucherEventPublisher eventPublisher,
        IJwtTokenService jwtTokenService,
        IConfiguration configuration)
    {
        _detailRepository = detailRepository;
        _customerRepository = customerRepository;
        _memberRepository = memberRepository;
        _transferRepository = transferRepository;
        _brandCustomerRepository = brandCustomerRepository;
        _planRepository = planRepository;
        _brandRepository = brandRepository;
        _distributionRepository = distributionRepository;
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _eventPublisher = eventPublisher;
        _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<InitiateTransferResult> InitiateAsync(
        Guid senderId,
        Guid voucherId,
        string? recipientPhone,
        string? recipientMemberId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        if (note?.Length > GiftMessagePolicy.MaxBodyLength)
            return new InitiateTransferResult(false, ErrorCode: "Validation",
                ErrorMessage: $"Your message is longer than {GiftMessagePolicy.MaxBodyLength} characters, so no gift was sent. Shorten it and send the gift again.");

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

        // Matrix row 8: the sender gate must live here as well, because the single-voucher
        // endpoint calls InitiateAsync directly and never passes through TransferService.
        var senderMember = await _memberRepository.GetByIdAsync(senderId, cancellationToken);
        var senderCustomer = senderMember != null
            ? await _customerRepository.GetByIdAsync(senderMember.CustomerId, cancellationToken)
            : null;
        if (senderMember != null && (senderCustomer == null || senderCustomer.Status == CustomerStatus.Blacklisted))
            return new InitiateTransferResult(false, ErrorCode: "SenderBlacklisted", ErrorMessage: "Transfers are not allowed for this account.");

        var recipient = await ResolveRecipientAsync(recipientPhone, recipientMemberId, cancellationToken);
        if (recipient == null)
            return new InitiateTransferResult(false, ErrorCode: "RecipientNotFound", ErrorMessage: "Recipient could not be resolved.");

        if (recipient.Id == senderId)
            return new InitiateTransferResult(false, ErrorCode: "SelfTransferNotAllowed", ErrorMessage: "Cannot transfer a voucher to yourself.");

        // Matrix rows 5-6 (gap #5): check the RECIPIENT before creating the transfer.
        // S2: platform-blacklisted recipients cannot receive new vouchers (P2: new action).
        var recipientCustomer = await _customerRepository.GetByIdAsync(recipient.CustomerId, cancellationToken);
        if (recipientCustomer == null || recipientCustomer.Status == CustomerStatus.Blacklisted)
            return new InitiateTransferResult(false, ErrorCode: "RecipientBlacklisted", ErrorMessage: "This transfer cannot be completed.");

        // S1: a block by the voucher's owning brand stops gifting INTO that brand's ecosystem.
        var voucherPlan = await _planRepository.GetByIdAsync(voucher.ParentId, cancellationToken);
        if (voucherPlan != null && await _brandCustomerRepository.IsBlockedAsync(voucherPlan.BrandId, recipientCustomer.Id, cancellationToken))
            return new InitiateTransferResult(false, ErrorCode: "RecipientBrandBlocked", ErrorMessage: "This transfer cannot be completed.");

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

        // CR-2026-09-10-31: the note travelling with the gift is the first line of its conversation,
        // saved by the same SaveChanges as the transfer row and the voucher lock.
        if (!string.IsNullOrWhiteSpace(note))
        {
            _transferRepository.AddMessage(GiftMessage.Create(
                transfer.Id, senderId, recipient.Id,
                GiftMessageDirection.FromSender, GiftMessageKind.GiftNote, note, now));
        }

        await _transferRepository.SaveChangesAsync(cancellationToken);

        // CR-2026-09-10-30 row 2 + 3: tell the sender honestly where the gift is going, and send
        // the accept link whenever the recipient has an email address to receive it. Having an email
        // is the only requirement — the magic link signs in a member who never set a password, which
        // is the normal state for anyone a brand auto-provisioned.
        var recipientName = string.IsNullOrWhiteSpace(recipientCustomer.FullName)
            ? recipientCustomer.PhoneNumber
            : recipientCustomer.FullName;
        var isRegistered = !string.IsNullOrEmpty(recipient.PasswordHash);
        var hasEmail = !string.IsNullOrWhiteSpace(recipientCustomer.Email);

        string deliveryStatus;
        string message;
        if (hasEmail)
        {
            deliveryStatus = GiftDeliveryStatus.Emailed;
            message = $"We emailed {recipientName} a secure link to accept your gift. We'll let you know the moment "
                + $"they decide — the gift stays reserved for {TransferExpiryDays} days.";

            await NotifyRecipientAsync(transfer, recipient.Id, recipientCustomer, senderId, voucherPlan, cancellationToken);
        }
        else if (!isRegistered)
        {
            deliveryStatus = GiftDeliveryStatus.HoldingNotRegistered;
            message = $"{recipientCustomer.PhoneNumber} isn't on NonCash yet, so we're holding your gift for "
                + $"{TransferExpiryDays} days. Please ask them to register with that phone number — we'll email "
                + "the accept link the moment they do.";
        }
        else
        {
            deliveryStatus = GiftDeliveryStatus.HoldingNoEmail;
            message = $"{recipientCustomer.PhoneNumber} has no email on file, so we can't send the accept link yet. "
                + $"Your gift is reserved for them for {TransferExpiryDays} days — ask them to sign in with that "
                + "phone number to accept it.";
        }

        return new InitiateTransferResult(
            true,
            TransferId: transfer.Id,
            DeliveryStatus: deliveryStatus,
            RecipientPhone: recipientCustomer.PhoneNumber,
            RecipientName: recipientName,
            Message: message);
    }

    public async Task<TransferActionResult> AcceptAsync(
        Guid transferId,
        Guid recipientId,
        string? recipientNote = null,
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

        var acceptResult = await _transferRepository.AcceptAsync(transferId, recipientId, recipientNote, cancellationToken);

        if (!acceptResult.Success)
            return acceptResult;

        var giftedVoucher = await _detailRepository.GetByIdAsync(transfer.VoucherId, cancellationToken);
        var giftedPlan = giftedVoucher != null ? await _planRepository.GetByIdAsync(giftedVoucher.ParentId, cancellationToken) : null;
        var recipientMember = await _memberRepository.GetByIdAsync(recipientId, cancellationToken);

        // Auto-link: accepting a gifted voucher of brand X makes the recipient brand X's customer.
        if (giftedPlan != null && recipientMember != null)
            await _brandCustomerRepository.EnsureAsync(giftedPlan.BrandId, recipientMember.CustomerId, BrandCustomerSource.GiftingAuto, null, cancellationToken);

        // The gift only becomes a distribution record once the recipient actually takes it.
        if (giftedVoucher != null)
        {
            var now = DateTime.UtcNow;
            await _distributionRepository.AddAsync(new VoucherDistribution
            {
                VoucherId = giftedVoucher.Id,
                MemberId = recipientId,
                Method = DistributionMethod.Transfer,
                DistributionDate = now
            }, cancellationToken);
            await _distributionRepository.SaveChangesAsync(cancellationToken);

            try
            {
                await _eventPublisher.PublishAsync(
                    "voucher.transferred",
                    giftedVoucher.Id,
                    (await ResolveContactAsync(recipientId, cancellationToken)).Phone,
                    giftedPlan?.BrandId,
                    new { fromMemberId = transfer.SenderId, toMemberId = recipientId, direction = "outgoing", transferId },
                    cancellationToken);
            }
            catch
            {
                // Best-effort: event publishing errors must not fail the acceptance.
            }
        }

        await NotifySenderAcceptedAsync(transfer, giftedPlan, recipientNote, cancellationToken);

        return acceptResult;
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

        var rejectResult = await _transferRepository.RejectAsync(transferId, recipientId, reason, cancellationToken);

        if (rejectResult.Success)
        {
            var giftedVoucher = await _detailRepository.GetByIdAsync(transfer.VoucherId, cancellationToken);
            var giftedPlan = giftedVoucher != null ? await _planRepository.GetByIdAsync(giftedVoucher.ParentId, cancellationToken) : null;
            await NotifySenderDeclinedAsync(transfer, giftedPlan, reason, cancellationToken);
        }

        return rejectResult;
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

    public Task<IReadOnlyList<BrandGiftItem>> GetBrandGiftsAsync(
        Guid? brandId,
        VoucherTransferStatus? status = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return _transferRepository.GetBrandGiftsAsync(brandId, status, page, pageSize, cancellationToken);
    }

    public async Task<GiftThreadResult> GetThreadAsync(
        Guid transferId,
        Guid viewerMemberId,
        CancellationToken cancellationToken = default)
    {
        var transfer = await _transferRepository.GetByIdAsync(transferId, cancellationToken);
        if (transfer == null)
            return new GiftThreadResult(false, ErrorCode: "TransferNotFound", ErrorMessage: GiftNotFoundMessage);

        if (transfer.SenderId != viewerMemberId && transfer.RecipientId != viewerMemberId)
            return new GiftThreadResult(false, ErrorCode: "Forbidden", ErrorMessage: NotAParticipantMessage);

        await _transferRepository.MarkMessagesReadAsync(transferId, viewerMemberId, cancellationToken);
        var messages = await _transferRepository.GetThreadAsync(transferId, cancellationToken);

        var closedReason = ClosedReasonFor(transfer);
        if (closedReason == null
            && await _transferRepository.CountMessagesAsync(transferId, cancellationToken) >= GiftMessagePolicy.MaxMessagesPerGift)
        {
            closedReason = GiftFullMessage;
        }

        return new GiftThreadResult(
            true,
            messages,
            transfer.Status.ToString(),
            CanReply: closedReason == null,
            ClosedReason: closedReason);
    }

    public async Task<GiftMessageResult> PostMessageAsync(
        Guid transferId,
        Guid authorMemberId,
        string? body,
        CancellationToken cancellationToken = default)
    {
        var transfer = await _transferRepository.GetByIdAsync(transferId, cancellationToken);
        if (transfer == null)
            return new GiftMessageResult(false, ErrorCode: "TransferNotFound", ErrorMessage: GiftNotFoundMessage);

        var isSender = transfer.SenderId == authorMemberId;
        if (!isSender && transfer.RecipientId != authorMemberId)
            return new GiftMessageResult(false, ErrorCode: "Forbidden", ErrorMessage: NotAParticipantMessage);

        // An overdue gift closes here rather than mid-write, so nobody adds a line to a dead thread.
        if (await _transferRepository.EnsureNotExpiredAsync(transferId, cancellationToken) != null)
            return new GiftMessageResult(false, ErrorCode: "ConversationClosed",
                ErrorMessage: ClosedMessage(VoucherTransferStatus.Expired, transfer.ExpiresAt, null));

        var trimmed = body?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            return new GiftMessageResult(false, ErrorCode: "Validation",
                ErrorMessage: "Your message is empty. Write something, then send it again.");

        if (trimmed.Length > GiftMessagePolicy.MaxBodyLength)
            return new GiftMessageResult(false, ErrorCode: "Validation",
                ErrorMessage: $"Your message is {trimmed.Length} characters and the limit is "
                    + $"{GiftMessagePolicy.MaxBodyLength}. Shorten it, then send it again.");

        if (!GiftMessagePolicy.IsOpenForFollowUp(transfer.Status))
            return new GiftMessageResult(false, ErrorCode: "ConversationClosed",
                ErrorMessage: ClosedMessage(transfer.Status, transfer.ExpiresAt, transfer.RespondedAt));

        if (await _transferRepository.CountMessagesAsync(transferId, cancellationToken) >= GiftMessagePolicy.MaxMessagesPerGift)
            return new GiftMessageResult(false, ErrorCode: "MessageLimitReached", ErrorMessage: GiftFullMessage);

        var now = DateTime.UtcNow;
        if (await _transferRepository.CountMessagesByAuthorSinceAsync(authorMemberId, now.AddHours(-1), cancellationToken)
            >= GiftMessagePolicy.MaxMessagesPerHour)
        {
            return new GiftMessageResult(false, ErrorCode: "RateLimited",
                ErrorMessage: $"You have sent {GiftMessagePolicy.MaxMessagesPerHour} messages in the last hour, which is "
                    + "the hourly limit. Wait a few minutes, then send this one again.");
        }

        var readerMemberId = isSender ? transfer.RecipientId : transfer.SenderId;
        var message = GiftMessage.Create(
            transferId,
            authorMemberId,
            readerMemberId,
            isSender ? GiftMessageDirection.FromSender : GiftMessageDirection.FromRecipient,
            GiftMessageKind.FollowUp,
            trimmed,
            now);

        _transferRepository.AddMessage(message);
        await _transferRepository.SaveChangesAsync(cancellationToken);

        var authorMember = await _memberRepository.GetByIdAsync(authorMemberId, cancellationToken);
        var item = new GiftMessageItem(
            message.Id,
            transferId,
            authorMemberId,
            authorMember?.FullName ?? "A member",
            message.Direction,
            message.Kind,
            message.Body,
            message.SentAt,
            message.IsRead);

        await NotifyMessageAsync(transfer, readerMemberId, item.AuthorDisplayName, message.Body, now, cancellationToken);

        return new GiftMessageResult(true, Message: item);
    }

    public Task<IReadOnlyList<GiftMessageItem>> GetBrandMessagesAsync(
        Guid transferId,
        Guid? brandId,
        CancellationToken cancellationToken = default)
    {
        return _transferRepository.GetBrandVisibleThreadAsync(transferId, brandId, cancellationToken);
    }

    private const string GiftNotFoundMessage =
        "We can't find that gift. Refresh your gift list and open it from there.";

    private const string NotAParticipantMessage =
        "Only the two people in this gift can read or write its messages. Open the gift from your own gift list.";

    private static readonly string GiftFullMessage =
        $"This gift already holds {GiftMessagePolicy.MaxMessagesPerGift} messages, the most one gift can keep. "
        + "You can still read the conversation, but nothing more can be added to it.";

    private static string? ClosedReasonFor(VoucherTransfer transfer) =>
        GiftMessagePolicy.IsOpenForFollowUp(transfer.Status)
            ? null
            : ClosedMessage(transfer.Status, transfer.ExpiresAt, transfer.RespondedAt);

    /// <summary>Why a conversation is closed, said in the words of the outcome that closed it.</summary>
    private static string ClosedMessage(VoucherTransferStatus status, DateTime expiresAt, DateTime? respondedAt)
    {
        var closedOn = (respondedAt ?? expiresAt).ToLocalTime().ToString("dd/MM/yyyy HH:mm");

        return status switch
        {
            VoucherTransferStatus.Rejected =>
                $"This gift was declined, so its conversation closed on {closedOn}. "
                + "The voucher went back to the sender — send a new gift to start a fresh one.",
            VoucherTransferStatus.Expired =>
                $"Nobody accepted this gift before {expiresAt.ToLocalTime():dd/MM/yyyy HH:mm}, so its conversation closed. "
                + "The voucher is back in the sender's wallet and can be sent again.",
            VoucherTransferStatus.Cancelled =>
                $"The sender cancelled this gift on {closedOn}, so its conversation closed. "
                + "The voucher is back in the sender's wallet and can be sent again.",
            _ =>
                $"This gift is {status}, so its conversation closed on {closedOn}."
        };
    }

    private async Task NotifyMessageAsync(
        VoucherTransfer transfer,
        Guid readerMemberId,
        string authorName,
        string body,
        DateTime sentAt,
        CancellationToken cancellationToken)
    {
        try
        {
            var reader = await ResolveContactAsync(readerMemberId, cancellationToken);
            var giftedVoucher = await _detailRepository.GetByIdAsync(transfer.VoucherId, cancellationToken);
            var giftedPlan = giftedVoucher != null ? await _planRepository.GetByIdAsync(giftedVoucher.ParentId, cancellationToken) : null;

            await _notificationService.NotifyGiftMessageAsync(new GiftMessageNotification(
                reader.Email,
                reader.Name,
                authorName,
                body,
                await DescribeVoucherAsync(giftedPlan, cancellationToken),
                giftedPlan?.FaceValue ?? 0m,
                sentAt), cancellationToken);
        }
        catch
        {
            // The message is stored either way; failing to announce it must not lose it.
        }
    }

    public async Task<int> SweepExpiredAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var expiring = (await _transferRepository.FindAsync(
            t => t.Status == VoucherTransferStatus.PendingAcceptance && t.ExpiresAt <= now,
            cancellationToken)).ToList();

        var expiredCount = await _transferRepository.SweepExpiredAsync(now, cancellationToken);

        foreach (var transfer in expiring)
        {
            var giftedVoucher = await _detailRepository.GetByIdAsync(transfer.VoucherId, cancellationToken);
            var giftedPlan = giftedVoucher != null ? await _planRepository.GetByIdAsync(giftedVoucher.ParentId, cancellationToken) : null;
            await NotifySenderExpiredAsync(transfer, giftedPlan, now, cancellationToken);
        }

        return expiredCount;
    }

    public async Task NotifyPendingGiftsAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var recipient = await _memberRepository.GetByIdAsync(memberId, cancellationToken);
        if (recipient == null)
            return;

        // An email address is the only requirement — the magic link signs in a member who has no password.
        var recipientCustomer = await _customerRepository.GetByIdAsync(recipient.CustomerId, cancellationToken);
        if (recipientCustomer == null || string.IsNullOrWhiteSpace(recipientCustomer.Email))
            return; // Nothing to write to yet: the gift stays held.

        var pending = await _transferRepository.FindAsync(
            t => t.RecipientId == memberId && t.Status == VoucherTransferStatus.PendingAcceptance,
            cancellationToken);

        foreach (var transfer in pending)
        {
            var giftedVoucher = await _detailRepository.GetByIdAsync(transfer.VoucherId, cancellationToken);
            var giftedPlan = giftedVoucher != null ? await _planRepository.GetByIdAsync(giftedVoucher.ParentId, cancellationToken) : null;
            await NotifyRecipientAsync(transfer, memberId, recipientCustomer, transfer.SenderId, giftedPlan, cancellationToken);
        }
    }

    private async Task NotifyRecipientAsync(
        VoucherTransfer transfer,
        Guid recipientMemberId,
        Customer recipientCustomer,
        Guid senderId,
        VoucherPlanHeader? plan,
        CancellationToken cancellationToken)
    {
        try
        {
            var sender = await ResolveContactAsync(senderId, cancellationToken);
            await _notificationService.NotifyVoucherTransferInitiatedAsync(new VoucherTransferInitiatedNotification(
                recipientCustomer.Email,
                recipientCustomer.PhoneNumber,
                string.IsNullOrWhiteSpace(recipientCustomer.FullName) ? recipientCustomer.PhoneNumber : recipientCustomer.FullName,
                sender.Name,
                1,
                transfer.InitiatedAt,
                BuildMagicLinkUrl(recipientMemberId)), cancellationToken);
        }
        catch
        {
            // Notification failures must not break gifting.
        }
    }

    private async Task NotifySenderAcceptedAsync(
        VoucherTransfer transfer,
        VoucherPlanHeader? plan,
        string? recipientNote,
        CancellationToken cancellationToken)
    {
        try
        {
            var sender = await ResolveContactAsync(transfer.SenderId, cancellationToken);
            var recipient = await ResolveContactAsync(transfer.RecipientId, cancellationToken);
            await _notificationService.NotifyGiftAcceptedAsync(new GiftAcceptedNotification(
                sender.Email,
                sender.Name,
                recipient.Name,
                recipientNote,
                await DescribeVoucherAsync(plan, cancellationToken),
                plan?.FaceValue ?? 0m,
                DateTime.UtcNow), cancellationToken);
        }
        catch
        {
            // Notification failures must not break the acceptance.
        }
    }

    private async Task NotifySenderDeclinedAsync(
        VoucherTransfer transfer,
        VoucherPlanHeader? plan,
        string? reason,
        CancellationToken cancellationToken)
    {
        try
        {
            var sender = await ResolveContactAsync(transfer.SenderId, cancellationToken);
            var recipient = await ResolveContactAsync(transfer.RecipientId, cancellationToken);
            await _notificationService.NotifyGiftDeclinedAsync(new GiftDeclinedNotification(
                sender.Email,
                sender.Name,
                recipient.Name,
                reason,
                await DescribeVoucherAsync(plan, cancellationToken),
                plan?.FaceValue ?? 0m,
                DateTime.UtcNow), cancellationToken);
        }
        catch
        {
            // Notification failures must not break the rejection.
        }
    }

    private async Task NotifySenderExpiredAsync(
        VoucherTransfer transfer,
        VoucherPlanHeader? plan,
        DateTime expiredAt,
        CancellationToken cancellationToken)
    {
        try
        {
            var sender = await ResolveContactAsync(transfer.SenderId, cancellationToken);
            var recipient = await ResolveContactAsync(transfer.RecipientId, cancellationToken);
            await _notificationService.NotifyGiftExpiredAsync(new GiftExpiredNotification(
                sender.Email,
                sender.Name,
                recipient.Name,
                recipient.Phone,
                await DescribeVoucherAsync(plan, cancellationToken),
                plan?.FaceValue ?? 0m,
                TransferExpiryDays,
                expiredAt), cancellationToken);
        }
        catch
        {
            // Notification failures must not break the sweep.
        }
    }

    private async Task<(string? Email, string Name, string Phone)> ResolveContactAsync(
        Guid memberId,
        CancellationToken cancellationToken)
    {
        var member = await _memberRepository.GetByIdAsync(memberId, cancellationToken);
        var customer = member != null ? await _customerRepository.GetByIdAsync(member.CustomerId, cancellationToken) : null;
        var name = !string.IsNullOrWhiteSpace(customer?.FullName)
            ? customer!.FullName
            : member?.FullName ?? "A member";
        return (customer?.Email, name, customer?.PhoneNumber ?? string.Empty);
    }

    private async Task<string> DescribeVoucherAsync(VoucherPlanHeader? plan, CancellationToken cancellationToken)
    {
        if (plan == null)
            return "your voucher";

        if (!string.IsNullOrWhiteSpace(plan.DisplayName))
            return plan.DisplayName!;

        var brandName = plan.Brand?.Name ?? (await _brandRepository.GetByIdAsync(plan.BrandId, cancellationToken))?.Name;
        return string.IsNullOrWhiteSpace(brandName) ? "your voucher" : $"{brandName} voucher";
    }

    /// <summary>CR-2026-09-07-19: Build a magic-link URL for passwordless member access.</summary>
    private string BuildMagicLinkUrl(Guid memberAccountId)
    {
        var magicToken = _jwtTokenService.GenerateMagicLinkToken(memberAccountId);
        var webBaseUrl = _configuration["WebBaseUrl"]?.TrimEnd('/') ?? "https://localhost:7162";
        return $"{webBaseUrl}/member/welcome?token={Uri.EscapeDataString(magicToken)}";
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
