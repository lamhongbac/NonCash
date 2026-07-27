using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Services;

/// <summary>
/// EF-backed settlement service managing cross-tenant ledger entries, manual settlement, and netting reports.
/// </summary>
public class SettlementService : ISettlementService
{
    private readonly ApplicationDbContext _context;

    public SettlementService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SettlementEntry?> CreateSettlementEntryAsync(
        VoucherUsage usage,
        Guid issuingBrandId,
        decimal faceValue,
        CancellationToken cancellationToken = default)
    {
        // Idempotency: skip if entry already exists for this usage
        var existing = await _context.Set<SettlementEntry>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.VoucherUsageId == usage.Id, cancellationToken);

        if (existing != null)
            return existing;

        var entry = new SettlementEntry
        {
            SponsorBrandId = usage.SponsorBrandId,
            IssuingBrandId = issuingBrandId,
            RedeemBrandId = usage.RedeemBrandId,
            RedeemOutletId = usage.PosId,
            VoucherUsageId = usage.Id,
            FaceValue = faceValue,
            Status = SettlementStatus.Pending
        };

        _context.Set<SettlementEntry>().Add(entry);
        await _context.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task<SettlementLedgerResult> GetLedgerAsync(
        SettlementFilters filters,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<SettlementEntry>()
            .Include(s => s.SponsorBrand)
            .Include(s => s.IssuingBrand)
            .Include(s => s.RedeemBrand)
            .Include(s => s.VoucherUsage)
            .AsNoTracking()
            .AsQueryable();

        if (filters.SponsorBrandId.HasValue)
            query = query.Where(s => s.SponsorBrandId == filters.SponsorBrandId.Value);

        if (filters.RedeemBrandId.HasValue)
            query = query.Where(s => s.RedeemBrandId == filters.RedeemBrandId.Value);

        if (filters.Status.HasValue)
            query = query.Where(s => s.Status == filters.Status.Value);

        if (filters.FromDate.HasValue)
            query = query.Where(s => s.CreatedAt >= filters.FromDate.Value);

        if (filters.ToDate.HasValue)
            query = query.Where(s => s.CreatedAt <= filters.ToDate.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var entries = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((filters.Page - 1) * filters.PageSize)
            .Take(filters.PageSize)
            .ToListAsync(cancellationToken);

        return new SettlementLedgerResult(entries, totalCount, filters.Page, filters.PageSize);
    }

    public async Task<bool> MarkSettledAsync(
        Guid entryId,
        Guid settledBy,
        CancellationToken cancellationToken = default)
    {
        var rowsAffected = await _context.Set<SettlementEntry>()
            .Where(s => s.Id == entryId && s.Status == SettlementStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Status, SettlementStatus.Settled)
                .SetProperty(s => s.SettledAt, DateTime.UtcNow)
                .SetProperty(s => s.SettledBy, settledBy)
                .SetProperty(s => s.UpdatedAt, DateTime.UtcNow),
                cancellationToken);

        return rowsAffected == 1;
    }

    public async Task<Dictionary<(Guid? SponsorBrandId, Guid? RedeemBrandId), decimal>> ComputeNettingAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var entries = await _context.Set<SettlementEntry>()
            .AsNoTracking()
            .Where(s => s.CreatedAt >= from && s.CreatedAt <= to)
            .Select(s => new { s.SponsorBrandId, s.RedeemBrandId, s.FaceValue })
            .ToListAsync(cancellationToken);

        return entries
            .GroupBy(s => (s.SponsorBrandId, s.RedeemBrandId))
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => x.FaceValue));
    }
}
