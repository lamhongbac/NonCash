using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Services;

/// <summary>
/// EF-backed prepaid credit service (Epic 9). Append-only ledger; balance = SUM(Amount).
/// Consumption is idempotent per voucher (unique index on voucher_detail_id) and never
/// blocks the calling business operation (grace overdraft — balance may go negative).
/// </summary>
public class CreditService : ICreditService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CreditService> _logger;

    public CreditService(ApplicationDbContext context, ILogger<CreditService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> GetBalanceAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        return await _context.CreditLedgerEntries
            .AsNoTracking()
            .Where(c => c.BrandId == brandId)
            .SumAsync(c => c.Amount, cancellationToken);
    }

    public async Task<bool> HasCreditAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        return await GetBalanceAsync(brandId, cancellationToken) > 0;
    }

    public async Task TryConsumeAsync(
        Guid brandId,
        Guid voucherDetailId,
        string? reference = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Idempotency pre-check: skip if this voucher was already charged.
            var alreadyCharged = await _context.CreditLedgerEntries
                .AsNoTracking()
                .AnyAsync(c => c.VoucherDetailId == voucherDetailId, cancellationToken);

            if (alreadyCharged)
                return;

            _context.CreditLedgerEntries.Add(new CreditLedgerEntry
            {
                BrandId = brandId,
                EntryType = CreditEntryType.Consumption,
                Amount = -1,
                Reference = reference,
                VoucherDetailId = voucherDetailId
            });

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Unique index violation = concurrent charge for the same voucher → idempotent success.
            _logger.LogWarning(ex,
                "Credit consumption skipped for voucher {VoucherDetailId} (brand {BrandId}) — likely already charged.",
                voucherDetailId, brandId);
        }
        catch (Exception ex)
        {
            // Billing must never break the business operation (grace policy).
            _logger.LogError(ex,
                "Credit consumption failed for voucher {VoucherDetailId} (brand {BrandId}).",
                voucherDetailId, brandId);
        }
    }

    public async Task<CreditLedgerEntry> TopUpAsync(
        Guid brandId,
        int amount,
        CreditEntryType type,
        string? reference,
        Guid? byUserId,
        CancellationToken cancellationToken = default)
    {
        if (type == CreditEntryType.Consumption)
            throw new ArgumentException("Consumption entries cannot be created via top-up.", nameof(type));
        if (amount == 0)
            throw new ArgumentException("Amount must not be zero.", nameof(amount));
        if (amount < 0 && type != CreditEntryType.Adjustment)
            throw new ArgumentException("Only Adjustment entries may have a negative amount.", nameof(amount));

        var entry = new CreditLedgerEntry
        {
            BrandId = brandId,
            EntryType = type,
            Amount = amount,
            Reference = reference,
            CreatedBy = byUserId
        };

        _context.CreditLedgerEntries.Add(entry);
        await _context.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task<CreditLedgerResult> GetLedgerAsync(
        CreditLedgerFilters filters,
        CancellationToken cancellationToken = default)
    {
        var query = _context.CreditLedgerEntries
            .Include(c => c.Brand)
            .AsNoTracking()
            .AsQueryable();

        if (filters.BrandId.HasValue)
            query = query.Where(c => c.BrandId == filters.BrandId.Value);

        if (filters.EntryType.HasValue)
            query = query.Where(c => c.EntryType == filters.EntryType.Value);

        if (filters.FromDate.HasValue)
            query = query.Where(c => c.CreatedAt >= filters.FromDate.Value);

        if (filters.ToDate.HasValue)
            query = query.Where(c => c.CreatedAt <= filters.ToDate.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var entries = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((filters.Page - 1) * filters.PageSize)
            .Take(filters.PageSize)
            .ToListAsync(cancellationToken);

        return new CreditLedgerResult(entries, totalCount, filters.Page, filters.PageSize);
    }
}
