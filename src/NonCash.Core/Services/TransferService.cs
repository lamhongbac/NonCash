using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

/// <summary>
/// Bulk gift sending. CR-2026-09-10-30: this no longer moves voucher ownership — every gift goes
/// through <see cref="IVoucherTransferService.InitiateAsync"/>, which reserves the voucher, records
/// it in voucher_transfers and tells the sender honestly whether the recipient was emailed.
/// Ownership changes only when the recipient accepts.
/// </summary>
public class TransferService : ITransferService
{
    private readonly IRepository<VoucherPlanDetail> _detailRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IMemberAccountRepository _memberRepository;
    private readonly IRepository<VoucherDistribution> _distributionRepository;
    private readonly IVoucherTransferService _voucherTransferService;

    public TransferService(
        IRepository<VoucherPlanDetail> detailRepository,
        ICustomerRepository customerRepository,
        IMemberAccountRepository memberRepository,
        IRepository<VoucherDistribution> distributionRepository,
        IVoucherTransferService voucherTransferService)
    {
        _detailRepository = detailRepository;
        _customerRepository = customerRepository;
        _memberRepository = memberRepository;
        _distributionRepository = distributionRepository;
        _voucherTransferService = voucherTransferService ?? throw new ArgumentNullException(nameof(voucherTransferService));
    }

    public async Task<TransferResult> TransferAsync(
        Guid fromMemberId,
        IReadOnlyList<Guid> voucherIds,
        IReadOnlyList<string> recipientPhones,
        string? note = null,
        CancellationToken cancellationToken = default)
    {
        if (voucherIds == null || voucherIds.Count == 0)
            return new TransferResult(false, ErrorCode: "EmptyList", ErrorMessage: "Voucher list is empty.");

        if (recipientPhones == null || recipientPhones.Count == 0)
            return new TransferResult(false, ErrorCode: "EmptyList", ErrorMessage: "Recipient phone list is empty.");

        // One note travels with every gift in this send, so it is judged once, before anything is reserved.
        if (note?.Length > GiftMessagePolicy.MaxBodyLength)
            return new TransferResult(
                false,
                ErrorCode: "Validation",
                ErrorMessage: $"Your message is longer than {GiftMessagePolicy.MaxBodyLength} characters, so no gift was sent. Shorten it and send the gifts again.");

        // AC3: 1-to-1 mapping required
        if (voucherIds.Count != recipientPhones.Count)
            return new TransferResult(
                false,
                ErrorCode: "MismatchedCounts",
                ErrorMessage: $"Voucher count ({voucherIds.Count}) must equal recipient phone count ({recipientPhones.Count}).");

        // Matrix row 8 (gap #3): a blacklisted sender cannot send gifts.
        var senderMember = await _memberRepository.GetByIdAsync(fromMemberId, cancellationToken);
        var senderCustomer = senderMember != null
            ? await _customerRepository.GetByIdAsync(senderMember.CustomerId, cancellationToken)
            : null;
        if (senderMember != null && (senderCustomer == null || senderCustomer.Status == CustomerStatus.Blacklisted))
            return new TransferResult(false, ErrorCode: "SenderBlacklisted", ErrorMessage: "Transfers are not allowed for this account.");

        // AC1 + NFR4: Load and validate ownership + status for each voucher
        var loaded = new List<VoucherPlanDetail>();
        foreach (var vid in voucherIds)
        {
            var detail = await _detailRepository.GetByIdAsync(vid, cancellationToken);
            if (detail == null)
                return new TransferResult(false, ErrorCode: "VoucherNotFound", ErrorMessage: $"Voucher {vid} not found.");

            if (detail.MemberId != fromMemberId)
                return new TransferResult(false, ErrorCode: "NotOwned", ErrorMessage: $"Voucher {detail.SerialNo} is not owned by you.");

            if (detail.UsageStatus != UsageStatus.Pending)
                return new TransferResult(false, ErrorCode: "NotTransferable", ErrorMessage: $"Voucher {detail.SerialNo} is not in Pending status.");

            loaded.Add(detail);
        }

        var skipped = new List<TransferSkipped>();
        var deliveries = new List<GiftDelivery>();

        for (var i = 0; i < loaded.Count; i++)
        {
            var rawPhone = recipientPhones[i] ?? string.Empty;
            var voucher = loaded[i];

            if (string.IsNullOrEmpty(Customer.NormalizePhoneNumber(rawPhone)))
            {
                skipped.Add(new TransferSkipped(rawPhone, voucher.Id, "InvalidPhoneNumber"));
                continue;
            }

            var initiated = await _voucherTransferService.InitiateAsync(
                fromMemberId, voucher.Id, rawPhone, recipientMemberId: null, note: note, cancellationToken);

            if (!initiated.Success)
            {
                skipped.Add(new TransferSkipped(rawPhone, voucher.Id, initiated.ErrorCode ?? "Rejected"));
                continue;
            }

            deliveries.Add(new GiftDelivery(
                voucher.Id,
                initiated.TransferId,
                initiated.RecipientPhone ?? rawPhone,
                initiated.RecipientName,
                initiated.DeliveryStatus ?? GiftDeliveryStatus.HoldingNoEmail,
                initiated.Message ?? string.Empty));
        }

        if (deliveries.Count == 0)
        {
            return new TransferResult(
                false,
                ErrorCode: "NoEligibleRecipients",
                ErrorMessage: "No gift could be sent — every recipient was skipped. Check the reason listed for each one.",
                SkippedRecords: skipped);
        }

        return new TransferResult(
            Success: true,
            TransferredCount: deliveries.Count,
            SkippedCount: skipped.Count,
            SkippedRecords: skipped,
            Deliveries: deliveries);
    }

    // AC5: outgoing gift history — distributions where Method=Transfer for vouchers ever owned by fromMemberId.
    // CR-2026-09-10-30: a distribution row is written when the recipient accepts, so this lists completed gifts.
    public async Task<IReadOnlyList<TransferHistoryItem>> GetOutgoingHistoryAsync(
        Guid fromMemberId,
        CancellationToken cancellationToken = default)
    {
        // MVP: list distributions with Method=Transfer where the recipient is NOT fromMemberId
        // and where there exists at least one prior distribution to fromMemberId for the same voucher.
        var allTransfers = (await _distributionRepository.FindAsync(
            d => d.Method == DistributionMethod.Transfer,
            cancellationToken)).ToList();

        if (allTransfers.Count == 0)
            return Array.Empty<TransferHistoryItem>();

        // Determine which voucher IDs were ever owned by fromMemberId via prior distributions
        var voucherIds = allTransfers.Select(d => d.VoucherId).Distinct().ToList();
        var ownedByFrom = new HashSet<Guid>();
        foreach (var vid in voucherIds)
        {
            var distros = (await _distributionRepository.FindAsync(
                d => d.VoucherId == vid,
                cancellationToken)).OrderBy(d => d.DistributionDate).ToList();
            // If fromMemberId appears as recipient in any earlier record (or via Sale/Promotion), treat it as outgoing
            if (distros.Any(d => d.MemberId == fromMemberId))
                ownedByFrom.Add(vid);
        }

        var outgoing = allTransfers
            .Where(d => ownedByFrom.Contains(d.VoucherId) && d.MemberId != fromMemberId)
            .OrderByDescending(d => d.DistributionDate)
            .ToList();

        // Resolve serial numbers and recipient phones
        var result = new List<TransferHistoryItem>();
        foreach (var d in outgoing)
        {
            var detail = await _detailRepository.GetByIdAsync(d.VoucherId, cancellationToken);
            var recipient = await _memberRepository.GetByIdAsync(d.MemberId, cancellationToken);
            var recipientCustomer = recipient != null
                ? await _customerRepository.GetByIdAsync(recipient.CustomerId, cancellationToken)
                : null;
            result.Add(new TransferHistoryItem(
                d.VoucherId,
                detail?.SerialNo ?? string.Empty,
                recipientCustomer?.PhoneNumber ?? string.Empty,
                d.DistributionDate));
        }

        return result;
    }
}
