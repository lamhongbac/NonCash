using Microsoft.EntityFrameworkCore;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Infrastructure.Data;

namespace NonCash.Infrastructure.Repositories;

public class CustomerRepository : Repository<Customer>, ICustomerRepository
{
    private readonly ApplicationDbContext _context;

    public CustomerRepository(ApplicationDbContext context) : base(context)
    {
        _context = context;
    }

    public async Task<Customer?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.PhoneNumber == phoneNumber, cancellationToken);
    }

    public async Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalized = email.Trim().ToLowerInvariant();
        return await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == normalized, cancellationToken);
    }

    public async Task<bool> PhoneNumberExistsAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .AnyAsync(c => c.PhoneNumber == phoneNumber, cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(string email, Guid? excludeCustomerId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var normalized = email.Trim().ToLowerInvariant();
        var query = _context.Customers
            .AsNoTracking()
            .Where(c => c.Email != null && c.Email.ToLower() == normalized);

        if (excludeCustomerId.HasValue)
            query = query.Where(c => c.Id != excludeCustomerId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<IEnumerable<Customer>> SearchAsync(string? search, CustomerStatus? status, CancellationToken cancellationToken = default, Guid? brandId = null)
    {
        return await BuildSearchQuery(search, status, brandId)
            .OrderBy(c => c.FullName)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(string? search, CustomerStatus? status, CancellationToken cancellationToken = default, Guid? brandId = null)
    {
        return await BuildSearchQuery(search, status, brandId).CountAsync(cancellationToken);
    }

    /// <summary>
    /// Shared filter for list/count: a single search term matched partially (contains)
    /// against FullName and Email (case-insensitive) and against PhoneNumber on its
    /// digits only (so formatted input like "090-123" still matches "090123"),
    /// combined with an optional status filter. When <paramref name="brandId"/> is
    /// set, only customers having a brand_customers mapping to that brand are returned.
    /// </summary>
    private IQueryable<Customer> BuildSearchQuery(string? search, CustomerStatus? status, Guid? brandId = null)
    {
        var query = _context.Customers.AsNoTracking().AsQueryable();

        if (brandId.HasValue)
        {
            var brand = brandId.Value;
            query = query.Where(c => _context.BrandCustomers.Any(bc => bc.BrandId == brand && bc.CustomerId == c.Id));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            var digits = new string(search.Where(char.IsDigit).ToArray());

            query = query.Where(c =>
                c.FullName.ToLower().Contains(term)
                || (c.Email != null && c.Email.ToLower().Contains(term))
                || (digits.Length > 0 && c.PhoneNumber.Contains(digits)));
        }

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        return query;
    }
}
