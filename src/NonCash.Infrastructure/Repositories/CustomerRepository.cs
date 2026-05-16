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

    public async Task<bool> PhoneNumberExistsAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .AnyAsync(c => c.PhoneNumber == phoneNumber, cancellationToken);
    }

    public async Task<IEnumerable<Customer>> SearchAsync(string? phoneNumber, string? name, string? email, CustomerStatus? status, CancellationToken cancellationToken = default)
    {
        var query = _context.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            query = query.Where(c => c.PhoneNumber == phoneNumber);
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(c => EF.Functions.Like(c.FullName, $"%{name}%"));
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            query = query.Where(c => EF.Functions.Like(c.Email ?? "", $"%{email}%"));
        }

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        return await query.OrderBy(c => c.FullName).ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(string? phoneNumber, string? name, string? email, CustomerStatus? status, CancellationToken cancellationToken = default)
    {
        var query = _context.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            query = query.Where(c => c.PhoneNumber == phoneNumber);
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(c => EF.Functions.Like(c.FullName, $"%{name}%"));
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            query = query.Where(c => EF.Functions.Like(c.Email ?? "", $"%{email}%"));
        }

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        return await query.CountAsync(cancellationToken);
    }
}
