using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public class TransferService : ITransferService
{
    private readonly IRepository<VoucherPlanDetail> _detailRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IMemberAccountRepository _memberRepository;
    private readonly IRepository<VoucherDistribution> _distributionRepository;

    public TransferService(
        IRepository<VoucherPlanDetail> detailRepository,
        ICustomerRepository customerRepository,
        IMemberAccountRepository memberRepository,
        IRepository<VoucherDistribution> distributionRepository)
    {
        _detailRepository = detailRepository;
        _customerRepository = customerRepository;
        _memberRepository = memberRepository;
        _distributionRepository = distributionRepository;
    }

    public async Task<TransferResult> TransferAsync(
        Guid fromMemberId,
        IReadOnlyList<Guid> voucherIds,
        IReadOnlyList<string> recipientPhones,
        CancellationToken cancellationToken = default)
    {
        if (voucherIds == null || voucherIds.Count == 0)
            return new TransferResult(false, ErrorCode: "EmptyList", ErrorMessage: "Voucher list is empty.");

        if (recipientPhones == null || recipientPhones.Count == 0)
            return new TransferResult(false, ErrorCode: "EmptyList", ErrorMessage: "Recipient phone list is empty.");

        // AC3: 1-to-1 mapping required
        if (voucherIds.Count != recipientPhones.Count)
            return new TransferResult(
                false,
                ErrorCode: "MismatchedCounts",
                ErrorMessage: $"Voucher count ({voucherIds.Count}) must equal recipient phone count ({recipientPhones.Count}).");

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

        // Normalize phones, preserve order (1-to-1 mapping)
        var normalized = recipientPhones
            .Select(p => Customer.NormalizePhoneNumber(p ?? string.Empty))
            .ToList();

        var skipped = new List<TransferSkipped>();
        var transfers = new List<(VoucherPlanDetail Detail, Guid RecipientMemberId, string Phone)>();

        for (var i = 0; i < loaded.Count; i++)
        {
            var phone = normalized[i];
            var voucher = loaded[i];

            if (string.IsNullOrEmpty(phone))
            {
                skipped.Add(new TransferSkipped(recipientPhones[i] ?? string.Empty, voucher.Id, "InvalidPhoneNumber"));
                continue;
            }

            var member = await ResolveOrCreateMemberAccountByPhoneAsync(phone, cancellationToken);
            if (member == null)
            {
                skipped.Add(new TransferSkipped(phone, voucher.Id, "RecipientNotFound"));
                continue;
            }

            var customer = await _customerRepository.GetByIdAsync(member.CustomerId, cancellationToken);
            if (customer == null || customer.Status == CustomerStatus.Blacklisted)
            {
                skipped.Add(new TransferSkipped(phone, voucher.Id, "Blacklisted"));
                continue;
            }

            if (member.Id == fromMemberId)
            {
                skipped.Add(new TransferSkipped(phone, voucher.Id, "SelfTransferNotAllowed"));
                continue;
            }

            transfers.Add((voucher, member.Id, phone));
        }

        if (transfers.Count == 0)
        {
            return new TransferResult(
                false,
                ErrorCode: "NoEligibleRecipients",
                ErrorMessage: "No eligible recipients (all blacklisted/invalid).",
                SkippedRecords: skipped);
        }

        // AC1 + AC2: Atomic update (single SaveChangesAsync wraps all changes in one tx)
        var now = DateTime.UtcNow;
        foreach (var (detail, recipientMemberId, _) in transfers)
        {
            detail.MemberId = recipientMemberId;
            _detailRepository.Update(detail);

            var distribution = new VoucherDistribution
            {
                VoucherId = detail.Id,
                MemberId = recipientMemberId,
                Method = DistributionMethod.Transfer,
                DistributionDate = now
            };
            await _distributionRepository.AddAsync(distribution, cancellationToken);
        }

        await _detailRepository.SaveChangesAsync(cancellationToken);

        return new TransferResult(
            Success: true,
            TransferredCount: transfers.Count,
            SkippedCount: skipped.Count,
            SkippedRecords: skipped);
    }

    // AC5: outgoing transfer history — distributions where Method=Transfer for vouchers ever owned by fromMemberId
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

    private async Task<MemberAccount?> ResolveOrCreateMemberAccountByPhoneAsync(string phone, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByPhoneNumberAsync(phone, cancellationToken);
        if (customer == null)
        {
            // Auto-onboard new customer + placeholder member account
            customer = new Customer
            {
                PhoneNumber = phone,
                FullName = phone,
                Status = CustomerStatus.Active
            };
            await _customerRepository.AddAsync(customer, cancellationToken);
            await _customerRepository.SaveChangesAsync(cancellationToken);
        }

        var member = await _memberRepository.GetByCustomerIdAsync(customer.Id, cancellationToken);
        if (member != null)
            return member;

        var placeholder = new MemberAccount
        {
            CustomerId = customer.Id,
            Username = phone,
            PasswordHash = string.Empty, // Not usable for login until registered
            FullName = customer.FullName,
            Status = MemberAccountStatus.Active
        };
        return await _memberRepository.AddAsync(placeholder, cancellationToken);
    }
}
