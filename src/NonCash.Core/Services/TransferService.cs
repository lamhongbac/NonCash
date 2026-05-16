using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public class TransferService : ITransferService
{
    private readonly IRepository<VoucherPlanDetail> _detailRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IRepository<VoucherDistribution> _distributionRepository;

    public TransferService(
        IRepository<VoucherPlanDetail> detailRepository,
        ICustomerRepository customerRepository,
        IRepository<VoucherDistribution> distributionRepository)
    {
        _detailRepository = detailRepository;
        _customerRepository = customerRepository;
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
        var transfers = new List<(VoucherPlanDetail Detail, Guid RecipientId, string Phone)>();

        for (var i = 0; i < loaded.Count; i++)
        {
            var phone = normalized[i];
            var voucher = loaded[i];

            if (string.IsNullOrEmpty(phone))
            {
                skipped.Add(new TransferSkipped(recipientPhones[i] ?? string.Empty, voucher.Id, "InvalidPhoneNumber"));
                continue;
            }

            var existing = await _customerRepository.GetByPhoneNumberAsync(phone, cancellationToken);
            if (existing == null)
            {
                // Create new customer (auto-onboarded recipient)
                var newCustomer = new Customer
                {
                    PhoneNumber = phone,
                    FullName = phone,
                    Status = CustomerStatus.Active
                };
                await _customerRepository.AddAsync(newCustomer, cancellationToken);
                transfers.Add((voucher, newCustomer.Id, phone));
            }
            else if (existing.Status == CustomerStatus.Blacklisted)
            {
                // AC4: skip blacklisted recipient
                skipped.Add(new TransferSkipped(phone, voucher.Id, "Blacklisted"));
            }
            else if (existing.Id == fromMemberId)
            {
                skipped.Add(new TransferSkipped(phone, voucher.Id, "SelfTransferNotAllowed"));
            }
            else
            {
                transfers.Add((voucher, existing.Id, phone));
            }
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
        foreach (var (detail, recipientId, _) in transfers)
        {
            detail.MemberId = recipientId;
            _detailRepository.Update(detail);

            var distribution = new VoucherDistribution
            {
                VoucherId = detail.Id,
                MemberId = recipientId,
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
    // Since VoucherDistribution stores recipient (MemberId), we infer outgoing by joining with detail history.
    // Simplification for MVP: return all Transfer distributions whose voucher's previous owner was fromMemberId.
    // Without a "from_member_id" column, we approximate: list Transfer distributions where the original creator/sender context is captured externally.
    // For MVP we surface ALL transfer-method distributions for vouchers currently or formerly assigned, filtered later by UI scope.
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
            var recipient = await _customerRepository.GetByIdAsync(d.MemberId, cancellationToken);
            result.Add(new TransferHistoryItem(
                d.VoucherId,
                detail?.SerialNo ?? string.Empty,
                recipient?.PhoneNumber ?? string.Empty,
                d.DistributionDate));
        }

        return result;
    }
}
