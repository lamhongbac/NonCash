using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public class DistributionReportService : IDistributionReportService
{
    private readonly IVoucherPlanRepository _planRepository;
    private readonly IRepository<VoucherPlanDetail> _detailRepository;
    private readonly IRepository<VoucherDistribution> _distributionRepository;
    private readonly IMemberAccountRepository _memberRepository;

    public DistributionReportService(
        IVoucherPlanRepository planRepository,
        IRepository<VoucherPlanDetail> detailRepository,
        IRepository<VoucherDistribution> distributionRepository,
        IMemberAccountRepository memberRepository)
    {
        _planRepository = planRepository;
        _detailRepository = detailRepository;
        _distributionRepository = distributionRepository;
        _memberRepository = memberRepository;
    }

    public async Task<DistributionSummary> GetSummaryAsync(
        Guid brandId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        // AC1 + NFR4: Brand-scoped plans only
        var plans = (await _planRepository.ListByBrandAsync(brandId, cancellationToken)).ToList();
        if (plans.Count == 0)
        {
            return new DistributionSummary(0, new MethodBreakdown(0, 0, 0), Array.Empty<PlanDistributionRow>());
        }

        var planIds = plans.Select(p => p.Id).ToHashSet();

        // Load all details for these plans (to map distribution -> plan)
        var allDetails = (await _detailRepository.GetAllAsync(cancellationToken))
            .Where(d => planIds.Contains(d.ParentId))
            .ToList();
        var detailToPlan = allDetails.ToDictionary(d => d.Id, d => d.ParentId);

        // AC5: date range filter on distribution date
        var fromUtc = from?.ToUniversalTime();
        var toUtc = to?.ToUniversalTime();

        var allDistributions = (await _distributionRepository.GetAllAsync(cancellationToken))
            .Where(d => detailToPlan.ContainsKey(d.VoucherId))
            .Where(d => !fromUtc.HasValue || d.DistributionDate >= fromUtc.Value)
            .Where(d => !toUtc.HasValue || d.DistributionDate <= toUtc.Value)
            .ToList();

        // Aggregate per plan
        var rows = new List<PlanDistributionRow>();
        var totalSale = 0;
        var totalPromo = 0;
        var totalXfer = 0;

        var byPlan = allDistributions
            .GroupBy(d => detailToPlan[d.VoucherId])
            .ToDictionary(g => g.Key, g => g.ToList());

        var now = DateTime.UtcNow;
        foreach (var plan in plans.OrderByDescending(p => p.PlanDate))
        {
            byPlan.TryGetValue(plan.Id, out var planDistros);
            planDistros ??= new List<VoucherDistribution>();

            var sale = planDistros.Count(d => d.Method == DistributionMethod.Sale);
            var promo = planDistros.Count(d => d.Method == DistributionMethod.Promotion);
            var xfer = planDistros.Count(d => d.Method == DistributionMethod.Transfer);
            var actual = sale + promo + xfer;

            totalSale += sale;
            totalPromo += promo;
            totalXfer += xfer;

            var pct = plan.TargetDistributed > 0
                ? Math.Round((double)actual / plan.TargetDistributed * 100.0, 2)
                : 0.0;

            // AC2: At-risk if actual < target AND deadline within 14 days
            var daysToExpiry = (plan.ExpiryDate - now).TotalDays;
            var isAtRisk = actual < plan.TargetDistributed && daysToExpiry <= 14 && daysToExpiry >= 0;

            rows.Add(new PlanDistributionRow(
                plan.Id,
                plan.PlanDate,
                plan.VoucherType.ToString(),
                plan.FaceValue,
                plan.TargetDistributed,
                actual,
                pct,
                plan.ExpiryDate,
                isAtRisk,
                new MethodBreakdown(sale, promo, xfer)));
        }

        return new DistributionSummary(
            TotalDistributed: totalSale + totalPromo + totalXfer,
            TotalByMethod: new MethodBreakdown(totalSale, totalPromo, totalXfer),
            Plans: rows);
    }

    public async Task<IReadOnlyList<DistributionDetailItem>> GetPlanDetailsAsync(
        Guid brandId,
        Guid planId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        // NFR4: Verify plan ownership
        var plan = await _planRepository.GetByIdAsync(planId, cancellationToken);
        if (plan == null || plan.BrandId != brandId)
            return Array.Empty<DistributionDetailItem>();

        var planDetails = (await _detailRepository.FindAsync(d => d.ParentId == planId, cancellationToken)).ToList();
        if (planDetails.Count == 0)
            return Array.Empty<DistributionDetailItem>();

        var detailIdSet = planDetails.Select(d => d.Id).ToHashSet();
        var detailMap = planDetails.ToDictionary(d => d.Id, d => d);

        var fromUtc = from?.ToUniversalTime();
        var toUtc = to?.ToUniversalTime();

        var distros = (await _distributionRepository.GetAllAsync(cancellationToken))
            .Where(d => detailIdSet.Contains(d.VoucherId))
            .Where(d => !fromUtc.HasValue || d.DistributionDate >= fromUtc.Value)
            .Where(d => !toUtc.HasValue || d.DistributionDate <= toUtc.Value)
            .OrderByDescending(d => d.DistributionDate)
            .ToList();

        // Resolve recipient info through MemberAccount.Customer
        var memberIds = distros.Select(d => d.MemberId).Distinct().ToList();
        var memberMap = new Dictionary<Guid, MemberAccount>();
        foreach (var mid in memberIds)
        {
            var member = await _memberRepository.GetByIdAsync(mid, cancellationToken);
            if (member != null) memberMap[mid] = member;
        }

        return distros.Select(d =>
        {
            detailMap.TryGetValue(d.VoucherId, out var detail);
            memberMap.TryGetValue(d.MemberId, out var member);
            return new DistributionDetailItem(
                d.VoucherId,
                detail?.SerialNo ?? string.Empty,
                d.Method.ToString(),
                d.DistributionDate,
                member?.Customer?.PhoneNumber,
                member?.Customer?.FullName ?? member?.FullName);
        }).ToList();
    }
}
