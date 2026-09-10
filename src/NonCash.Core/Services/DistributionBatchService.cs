using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public class DistributionBatchService : IDistributionBatchService
{
    private readonly IVoucherPlanRepository _planRepository;
    private readonly IRepository<VoucherDistributionBatch> _batchRepository;
    private readonly IRepository<VoucherDistribution> _distributionRepository;
    private readonly IRepository<VoucherPlanDetail> _detailRepository;
    private readonly IMemberAccountRepository _memberRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IUserAccountRepository _userRepository;
    private readonly IBrandCustomerRepository _brandCustomerRepository;
    private readonly IBrandRepository _brandRepository;
    private readonly IOutletRepository _outletRepository;

    public DistributionBatchService(
        IVoucherPlanRepository planRepository,
        IRepository<VoucherDistributionBatch> batchRepository,
        IRepository<VoucherDistribution> distributionRepository,
        IRepository<VoucherPlanDetail> detailRepository,
        IMemberAccountRepository memberRepository,
        ICustomerRepository customerRepository,
        IUserAccountRepository userRepository,
        IBrandCustomerRepository brandCustomerRepository,
        IBrandRepository brandRepository,
        IOutletRepository outletRepository)
    {
        _planRepository = planRepository;
        _batchRepository = batchRepository;
        _distributionRepository = distributionRepository;
        _detailRepository = detailRepository;
        _memberRepository = memberRepository;
        _customerRepository = customerRepository;
        _userRepository = userRepository;
        _brandCustomerRepository = brandCustomerRepository;
        _brandRepository = brandRepository;
        _outletRepository = outletRepository;
    }

    public async Task<IReadOnlyList<DistributionBatchSummary>> GetBatchesByPlanAsync(
        Guid planId, Guid brandId, CancellationToken cancellationToken = default)
    {
        // Brand-scoped: a brand manager only ever sees runs of their own plans.
        var plan = await _planRepository.GetByIdAsync(planId, cancellationToken);
        if (plan == null || plan.BrandId != brandId)
            return Array.Empty<DistributionBatchSummary>();

        var batches = (await _batchRepository.FindAsync(b => b.PlanId == planId, cancellationToken))
            .OrderByDescending(b => b.CreatedAt)
            .ToList();
        if (batches.Count == 0)
            return Array.Empty<DistributionBatchSummary>();

        var creatorNames = await ResolveCreatorNamesAsync(
            batches.Where(b => b.CreatedById.HasValue).Select(b => b.CreatedById!.Value), cancellationToken);

        var planName = PlanDisplayName(plan);
        var scopeSummary = await BuildScopeSummaryAsync(plan, cancellationToken);
        return batches
            .Select(b => ToSummary(b, planName, creatorNames, scopeSummary))
            .ToList();
    }

    public async Task<DistributionBatchDetail?> GetBatchDetailAsync(
        Guid batchId, Guid brandId, CancellationToken cancellationToken = default)
    {
        var batch = (await _batchRepository.FindAsync(b => b.Id == batchId, cancellationToken)).FirstOrDefault();
        if (batch == null || batch.BrandId != brandId)
            return null;

        var plan = await _planRepository.GetByIdAsync(batch.PlanId, cancellationToken);
        var planName = plan != null ? PlanDisplayName(plan) : string.Empty;
        var scopeSummary = plan != null ? await BuildScopeSummaryAsync(plan, cancellationToken) : string.Empty;

        var creatorNames = await ResolveCreatorNamesAsync(
            batch.CreatedById.HasValue ? new[] { batch.CreatedById.Value } : Array.Empty<Guid>(), cancellationToken);
        var summary = ToSummary(batch, planName, creatorNames, scopeSummary);

        var distributions = (await _distributionRepository.FindAsync(d => d.BatchId == batchId, cancellationToken))
            .OrderByDescending(d => d.DistributionDate)
            .ToList();

        var voucherIds = distributions.Select(d => d.VoucherId).Distinct().ToList();
        var details = (await _detailRepository.FindAsync(v => voucherIds.Contains(v.Id), cancellationToken))
            .ToDictionary(v => v.Id);

        var recipients = await ResolveRecipientsAsync(distributions.Select(d => d.MemberId), cancellationToken);

        var rows = distributions.Select(d =>
        {
            details.TryGetValue(d.VoucherId, out var detail);
            recipients.TryGetValue(d.MemberId, out var recipient);
            var status = detail != null && plan != null
                ? DeriveStatus(detail, plan.ExpiryDate)
                : "Unknown";
            return new BatchRecipientRow(
                d.VoucherId,
                detail?.SerialNo ?? string.Empty,
                recipient.Phone,
                recipient.Name,
                status,
                d.Method.ToString(),
                d.DistributionDate,
                detail?.UsedDate);
        }).ToList();

        return new DistributionBatchDetail(summary, batch.SkippedRecords, rows);
    }

    public async Task<IReadOnlyList<PlanVoucherLedgerRow>> GetPlanVoucherLedgerAsync(
        Guid planId, Guid brandId, CancellationToken cancellationToken = default)
    {
        var plan = await _planRepository.GetByIdAsync(planId, cancellationToken);
        if (plan == null || plan.BrandId != brandId)
            return Array.Empty<PlanVoucherLedgerRow>();

        var details = (await _detailRepository.FindAsync(d => d.ParentId == planId, cancellationToken))
            .OrderBy(d => d.SerialNo)
            .ToList();
        if (details.Count == 0)
            return Array.Empty<PlanVoucherLedgerRow>();

        var latestByVoucher = await GetLatestDistributionsByVoucherAsync(details.Select(d => d.Id), cancellationToken);
        var recipients = await ResolveRecipientsAsync(
            details.Where(d => d.MemberId.HasValue).Select(d => d.MemberId!.Value), cancellationToken);

        return details.Select(d =>
        {
            latestByVoucher.TryGetValue(d.Id, out var distribution);
            var recipient = d.MemberId.HasValue && recipients.TryGetValue(d.MemberId.Value, out var r)
                ? r
                : (Phone: (string?)null, Name: (string?)null);
            return new PlanVoucherLedgerRow(
                d.Id,
                d.SerialNo,
                DeriveStatus(d, plan.ExpiryDate),
                d.UsedDate,
                d.VoucherCodeSecret,
                recipient.Phone,
                recipient.Name,
                distribution?.Method.ToString(),
                distribution?.DistributionDate,
                distribution?.BatchId);
        }).ToList();
    }

    public async Task<IReadOnlyList<CustomerVoucherHistoryRow>?> GetCustomerVouchersAsync(
        Guid customerId, Guid brandId, CancellationToken cancellationToken = default)
    {
        // The customer must be mapped to the caller's brand; otherwise this looks like a 404.
        var mapping = await _brandCustomerRepository.FindAsync(brandId, customerId, cancellationToken);
        if (mapping == null)
            return null;

        var members = (await _memberRepository.FindAsync(m => m.CustomerId == customerId, cancellationToken)).ToList();
        if (members.Count == 0)
            return new List<CustomerVoucherHistoryRow>();

        var memberIds = members.Select(m => m.Id).ToList();
        var details = (await _detailRepository.FindAsync(
                d => d.MemberId != null && memberIds.Contains(d.MemberId.Value), cancellationToken))
            .ToList();
        if (details.Count == 0)
            return new List<CustomerVoucherHistoryRow>();

        var planIds = details.Select(d => d.ParentId).Distinct().ToList();
        // Brand-scoped history: vouchers from other brands' plans are hidden even when the
        // same customer receives them (e.g. after shopping at two brands).
        var plans = (await _planRepository.FindAsync(p => planIds.Contains(p.Id), cancellationToken))
            .Where(p => p.BrandId == brandId)
            .ToDictionary(p => p.Id);

        var latestByVoucher = await GetLatestDistributionsByVoucherAsync(details.Select(d => d.Id), cancellationToken);

        return details
            .Where(d => plans.ContainsKey(d.ParentId))
            .Select(d =>
            {
                var plan = plans[d.ParentId];
                latestByVoucher.TryGetValue(d.Id, out var distribution);
                return new CustomerVoucherHistoryRow(
                    d.Id,
                    d.SerialNo,
                    plan.Id,
                    PlanDisplayName(plan),
                    plan.VoucherType.ToString(),
                    plan.FaceValue,
                    DeriveStatus(d, plan.ExpiryDate),
                    distribution?.DistributionDate,
                    distribution?.Method.ToString(),
                    d.UsedDate,
                    plan.ExpiryDate,
                    d.VoucherCodeSecret);
            })
            .OrderByDescending(r => r.DistributedAt ?? DateTime.MinValue)
            .ToList();
    }

    /// <summary>
    /// Lifecycle status shown to brand staff: InStock (not yet assigned) → Distributed →
    /// Redeeming (locked at POS) → Redeemed; a still-unredeemed voucher past the plan
    /// expiry shows as Expired.
    /// </summary>
    internal static string DeriveStatus(VoucherPlanDetail detail, DateTime planExpiryDate)
    {
        if (detail.MemberId == null)
            return "InStock";

        return detail.UsageStatus switch
        {
            UsageStatus.Complete => "Redeemed",
            UsageStatus.InUse => "Redeeming",
            _ => planExpiryDate.Date < DateTime.UtcNow.Date ? "Expired" : "Distributed",
        };
    }

    private static string PlanDisplayName(VoucherPlanHeader plan)
        => string.IsNullOrWhiteSpace(plan.DisplayName) ? $"{plan.VoucherType} Voucher" : plan.DisplayName!;

    private static DistributionBatchSummary ToSummary(
        VoucherDistributionBatch batch, string planName, IReadOnlyDictionary<Guid, string> creatorNames, string scopeSummary)
        => new(
            batch.Id,
            batch.PlanId,
            planName,
            batch.CreatedAt,
            batch.CreatedById.HasValue && creatorNames.TryGetValue(batch.CreatedById.Value, out var name)
                ? name
                : null,
            batch.NotifyChannel.ToString(),
            batch.RecipientCount,
            batch.DistributedCount,
            batch.SkippedCount,
            scopeSummary);

    /// <summary>
    /// Human-readable store scope of a plan: empty <see cref="VoucherScope.Outlets"/> means the
    /// whole brand (all its stores); otherwise the selected stores are named explicitly.
    /// </summary>
    private async Task<string> BuildScopeSummaryAsync(VoucherPlanHeader plan, CancellationToken cancellationToken)
    {
        if (plan.Scope.Outlets.Count == 0)
        {
            var brand = await _brandRepository.GetByIdAsync(plan.BrandId, cancellationToken);
            return string.IsNullOrWhiteSpace(brand?.Name)
                ? "All stores of the brand"
                : $"All stores of {brand!.Name}";
        }

        var outlets = await _outletRepository.FindAsync(o => plan.Scope.Outlets.Contains(o.Id), cancellationToken);
        var names = outlets.Select(o => o.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        return names.Count == 0
            ? $"{plan.Scope.Outlets.Count} selected store(s)"
            : $"{names.Count} selected store(s): {string.Join(", ", names)}";
    }

    private async Task<Dictionary<Guid, string>> ResolveCreatorNamesAsync(
        IEnumerable<Guid> creatorIds, CancellationToken cancellationToken)
    {
        var ids = creatorIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, string>();

        return (await _userRepository.FindAsync(u => ids.Contains(u.Id), cancellationToken))
            .ToDictionary(u => u.Id, u => u.FullName);
    }

    /// <summary>Resolves member accounts to (phone, name) via their linked customer record.</summary>
    private async Task<Dictionary<Guid, (string? Phone, string? Name)>> ResolveRecipientsAsync(
        IEnumerable<Guid> memberIds, CancellationToken cancellationToken)
    {
        var ids = memberIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, (string?, string?)>();

        var members = await _memberRepository.FindAsync(m => ids.Contains(m.Id), cancellationToken);
        var customerIds = members.Select(m => m.CustomerId).Distinct().ToList();
        var customers = (await _customerRepository.FindAsync(c => customerIds.Contains(c.Id), cancellationToken))
            .ToDictionary(c => c.Id);

        return members.ToDictionary(
            m => m.Id,
            m => customers.TryGetValue(m.CustomerId, out var customer)
                ? ((string?)customer.PhoneNumber, (string?)customer.FullName)
                : (null, string.IsNullOrWhiteSpace(m.FullName) ? null : m.FullName));
    }

    /// <summary>Latest distribution row per voucher (a voucher can be transferred, producing several rows).</summary>
    private async Task<Dictionary<Guid, VoucherDistribution>> GetLatestDistributionsByVoucherAsync(
        IEnumerable<Guid> voucherIds, CancellationToken cancellationToken)
    {
        var ids = voucherIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, VoucherDistribution>();

        return (await _distributionRepository.FindAsync(d => ids.Contains(d.VoucherId), cancellationToken))
            .GroupBy(d => d.VoucherId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.DistributionDate).First());
    }
}
