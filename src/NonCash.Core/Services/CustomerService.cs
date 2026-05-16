using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public class CustomerService
{
    private readonly ICustomerRepository _customerRepository;

    public CustomerService(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
    }

    public async Task<Customer> CreateAsync(string phoneNumber, string fullName, string? email, CancellationToken cancellationToken = default)
    {
        var normalizedPhone = Customer.NormalizePhoneNumber(phoneNumber);
        if (string.IsNullOrEmpty(normalizedPhone))
            throw new ArgumentException("Phone number is required.", nameof(phoneNumber));

        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));

        if (await _customerRepository.PhoneNumberExistsAsync(normalizedPhone, cancellationToken))
            throw new InvalidOperationException($"A customer with phone number '{normalizedPhone}' already exists.");

        var customer = new Customer
        {
            PhoneNumber = normalizedPhone,
            FullName = fullName.Trim(),
            Email = email?.Trim(),
            Status = CustomerStatus.Active
        };

        await _customerRepository.AddAsync(customer, cancellationToken);
        await _customerRepository.SaveChangesAsync(cancellationToken);
        return customer;
    }

    public async Task<Customer> UpdateAsync(Guid id, string fullName, string? email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));

        var customer = await _customerRepository.GetByIdAsync(id, cancellationToken);
        if (customer == null)
            throw new KeyNotFoundException($"Customer with ID '{id}' was not found.");

        customer.FullName = fullName.Trim();
        customer.Email = email?.Trim();

        await _customerRepository.SaveChangesAsync(cancellationToken);
        return customer;
    }

    public async Task<Customer> BlacklistAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByIdAsync(id, cancellationToken);
        if (customer == null)
            throw new KeyNotFoundException($"Customer with ID '{id}' was not found.");

        customer.Status = CustomerStatus.Blacklisted;

        await _customerRepository.SaveChangesAsync(cancellationToken);
        return customer;
    }

    public async Task<Customer> UnblacklistAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByIdAsync(id, cancellationToken);
        if (customer == null)
            throw new KeyNotFoundException($"Customer with ID '{id}' was not found.");

        customer.Status = CustomerStatus.Active;

        await _customerRepository.SaveChangesAsync(cancellationToken);
        return customer;
    }

    public async Task<(IEnumerable<Customer> Items, int TotalCount)> SearchAsync(
        string? phoneNumber,
        string? name,
        string? email,
        CustomerStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPhone = !string.IsNullOrWhiteSpace(phoneNumber)
            ? Customer.NormalizePhoneNumber(phoneNumber)
            : null;

        var allItems = await _customerRepository.SearchAsync(normalizedPhone, name, email, status, cancellationToken);
        var totalCount = await _customerRepository.CountAsync(normalizedPhone, name, email, status, cancellationToken);

        var items = allItems
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _customerRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<bool> IsBlacklisted(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId, cancellationToken);
        return customer?.Status == CustomerStatus.Blacklisted;
    }

    public async Task<CustomerImportResult> UpsertAsync(IEnumerable<CustomerImportRecord> records, CancellationToken cancellationToken = default)
    {
        var created = 0;
        var updated = 0;
        var errors = new List<string>();

        foreach (var record in records)
        {
            try
            {
                var normalizedPhone = Customer.NormalizePhoneNumber(record.PhoneNumber);
                if (string.IsNullOrEmpty(normalizedPhone))
                {
                    errors.Add($"Invalid phone number: '{record.PhoneNumber}'");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(record.FullName))
                {
                    errors.Add($"Full name is required for phone: '{record.PhoneNumber}'");
                    continue;
                }

                var existing = await _customerRepository.GetByPhoneNumberAsync(normalizedPhone, cancellationToken);
                if (existing != null)
                {
                    existing.FullName = record.FullName.Trim();
                    existing.Email = record.Email?.Trim();
                    updated++;
                }
                else
                {
                    var customer = new Customer
                    {
                        PhoneNumber = normalizedPhone,
                        FullName = record.FullName.Trim(),
                        Email = record.Email?.Trim(),
                        Status = CustomerStatus.Active
                    };
                    await _customerRepository.AddAsync(customer, cancellationToken);
                    created++;
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Error processing '{record.PhoneNumber}': {ex.Message}");
            }
        }

        await _customerRepository.SaveChangesAsync(cancellationToken);

        return new CustomerImportResult(created, updated, errors);
    }
}

public record CustomerImportRecord(string PhoneNumber, string FullName, string? Email);

public record CustomerImportResult(int Created, int Updated, IReadOnlyList<string> Errors);
