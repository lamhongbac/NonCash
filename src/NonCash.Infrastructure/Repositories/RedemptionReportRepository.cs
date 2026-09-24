using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Repositories;

/// <summary>
/// Read-only redemption report over voucher_usages joined back to the plan for face value.
/// No schema change: the redeeming outlet is voucher_usages.pos_id and the bill number is
/// parsed out of TransactionId ("{StoreCode}-{Bill}-{ticks}").
/// </summary>
public class RedemptionReportRepository : IRedemptionReportRepository
{
    private readonly ApplicationDbContext _context;

    public RedemptionReportRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RedemptionReportPage> GetReportAsync(
        Guid? brandId,
        Guid? outletId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = from u in _context.Set<VoucherUsage>()
                    join d in _context.Set<VoucherPlanDetail>() on u.VoucherId equals d.Id
                    join h in _context.Set<VoucherPlanHeader>() on d.ParentId equals h.Id
                    join o in _context.Set<Outlet>() on u.PosId equals o.Id into outlets
                    from o in outlets.DefaultIfEmpty()
                    select new { Usage = u, Header = h, Detail = d, Outlet = o };

        if (brandId.HasValue)
            query = query.Where(x => x.Header.BrandId == brandId.Value);

        if (outletId.HasValue)
            query = query.Where(x => x.Usage.PosId == outletId.Value);

        if (from.HasValue)
            query = query.Where(x => x.Usage.UsageDate >= from.Value.Date);

        // "to" is inclusive of the whole day: UsageDate < next-day midnight.
        if (to.HasValue)
            query = query.Where(x => x.Usage.UsageDate < to.Value.Date.AddDays(1));

        // Totals across the whole filtered set. Face-value aggregates only cover Value-type
        // rows: a Percentage plan's FaceValue is a percent number, not money.
        var totals = await query
            .GroupBy(x => 1)
            .Select(g => new
            {
                TotalCount = g.Count(),
                TotalAmountUsed = g.Sum(x => x.Usage.AmountUsed),
                TotalFaceValue = g.Sum(x => x.Header.ValueType == VoucherValueType.Value ? x.Header.FaceValue : 0m),
                TotalBreakage = g.Sum(x => x.Header.ValueType == VoucherValueType.Value ? x.Header.FaceValue - x.Usage.AmountUsed : 0m)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var rawRows = await query
            .OrderByDescending(x => x.Usage.UsageDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Usage.Id,
                x.Usage.UsageDate,
                x.Usage.TransactionId,
                x.Detail.SerialNo,
                x.Header.ValueType,
                x.Header.FaceValue,
                x.Usage.AmountUsed,
                x.Usage.PosId,
                OutletName = x.Outlet != null ? x.Outlet.Name : null,
                x.Usage.PosNo,
                x.Usage.OperatorId
            })
            .ToListAsync(cancellationToken);

        var items = rawRows
            .Select(r => new RedemptionReportRow(
                r.Id,
                r.UsageDate,
                r.TransactionId,
                TryExtractBillNumber(r.TransactionId),
                r.SerialNo,
                r.ValueType.ToString(),
                r.FaceValue,
                r.AmountUsed,
                r.ValueType == VoucherValueType.Value ? r.FaceValue - r.AmountUsed : (decimal?)null,
                r.PosId,
                r.OutletName,
                r.PosNo,
                r.OperatorId))
            .ToList();

        return new RedemptionReportPage(
            items,
            totals?.TotalCount ?? 0,
            page,
            pageSize,
            totals?.TotalFaceValue ?? 0m,
            totals?.TotalAmountUsed ?? 0m,
            totals?.TotalBreakage ?? 0m);
    }

    public async Task<IReadOnlyList<RedemptionReportRow>> GetRowsForExportAsync(
        Guid? brandId,
        Guid? outletId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var query = from u in _context.Set<VoucherUsage>()
                    join d in _context.Set<VoucherPlanDetail>() on u.VoucherId equals d.Id
                    join h in _context.Set<VoucherPlanHeader>() on d.ParentId equals h.Id
                    join o in _context.Set<Outlet>() on u.PosId equals o.Id into outlets
                    from o in outlets.DefaultIfEmpty()
                    select new { Usage = u, Header = h, Detail = d, Outlet = o };

        if (brandId.HasValue)
            query = query.Where(x => x.Header.BrandId == brandId.Value);

        if (outletId.HasValue)
            query = query.Where(x => x.Usage.PosId == outletId.Value);

        if (from.HasValue)
            query = query.Where(x => x.Usage.UsageDate >= from.Value.Date);

        // "to" is inclusive of the whole day: UsageDate < next-day midnight.
        if (to.HasValue)
            query = query.Where(x => x.Usage.UsageDate < to.Value.Date.AddDays(1));

        var rawRows = await query
            .OrderByDescending(x => x.Usage.UsageDate)
            .Select(x => new
            {
                x.Usage.Id,
                x.Usage.UsageDate,
                x.Usage.TransactionId,
                x.Detail.SerialNo,
                x.Header.ValueType,
                x.Header.FaceValue,
                x.Usage.AmountUsed,
                x.Usage.PosId,
                OutletName = x.Outlet != null ? x.Outlet.Name : null,
                x.Usage.PosNo,
                x.Usage.OperatorId
            })
            .ToListAsync(cancellationToken);

        return rawRows
            .Select(r => new RedemptionReportRow(
                r.Id,
                r.UsageDate,
                r.TransactionId,
                TryExtractBillNumber(r.TransactionId),
                r.SerialNo,
                r.ValueType.ToString(),
                r.FaceValue,
                r.AmountUsed,
                r.ValueType == VoucherValueType.Value ? r.FaceValue - r.AmountUsed : (decimal?)null,
                r.PosId,
                r.OutletName,
                r.PosNo,
                r.OperatorId))
            .ToList();
    }

    public async Task<IReadOnlyList<RedemptionOutletOption>> GetOutletsForBrandAsync(
        Guid brandId,
        CancellationToken cancellationToken = default)
    {
        var outlets = await _context.Set<Outlet>()
            .AsNoTracking()
            .Where(o => o.BrandId == brandId)
            .OrderBy(o => o.Name)
            .Select(o => new RedemptionOutletOption(o.Id, o.Name, o.Code))
            .ToListAsync(cancellationToken);

        return outlets;
    }

    /// <summary>
    /// POS clients mint TransactionId as "{StoreCode}-{Bill}-{ticks}". Recover the bill number
    /// only when that shape holds; anything else is shown raw by the caller.
    /// </summary>
    private static string? TryExtractBillNumber(string transactionId)
    {
        var parts = transactionId.Split('-');
        if (parts.Length < 3)
            return null;

        if (!long.TryParse(parts[^1], out _))
            return null;

        return string.Join('-', parts.Skip(1).Take(parts.Length - 2));
    }
}
