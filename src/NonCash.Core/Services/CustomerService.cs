using NonCash.Core.Entities;
using NonCash.Core.Interfaces;

namespace NonCash.Core.Services;

public class CustomerService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IBrandCustomerRepository _brandCustomerRepository;
    private readonly IRepository<CustomerAuditLog> _auditLogRepository;

    public CustomerService(ICustomerRepository customerRepository, IBrandCustomerRepository brandCustomerRepository, IRepository<CustomerAuditLog> auditLogRepository)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _brandCustomerRepository = brandCustomerRepository ?? throw new ArgumentNullException(nameof(brandCustomerRepository));
        _auditLogRepository = auditLogRepository ?? throw new ArgumentNullException(nameof(auditLogRepository));
    }

    public async Task<Customer> CreateAsync(string phoneNumber, string fullName, string? email, CancellationToken cancellationToken = default, Guid? brandId = null, Guid? createdBy = null)
    {
        var normalizedPhone = Customer.NormalizePhoneNumber(phoneNumber);
        if (string.IsNullOrEmpty(normalizedPhone))
            throw new ArgumentException("Phone number is required.", nameof(phoneNumber));

        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));

        var normalizedEmail = Customer.NormalizeEmail(email);

        // Global identity: a phone belongs to at most one customer platform-wide.
        var existing = await _customerRepository.GetByPhoneNumberAsync(normalizedPhone, cancellationToken);
        if (existing != null)
        {
            // Admin path (no brand scope): a global duplicate is a real conflict.
            if (!brandId.HasValue)
                throw new InvalidOperationException($"A customer with phone number '{normalizedPhone}' already exists.");

            // Brand path: a duplicate only when the brand mapping already exists;
            // otherwise link the existing global customer to this brand.
            var mapping = await _brandCustomerRepository.FindAsync(brandId.Value, existing.Id, cancellationToken);
            if (mapping != null)
                throw new InvalidOperationException($"A customer with phone number '{normalizedPhone}' already exists in your customer list.");

            // CR-2026-09-06-11 fill-empty-only: linking may populate empty/placeholder
            // fields but must never overwrite existing values.
            var dirty = false;
            if (existing.HasPlaceholderName())
            {
                existing.FullName = fullName.Trim();
                dirty = true;
            }

            if (normalizedEmail != null && string.IsNullOrWhiteSpace(existing.Email))
            {
                if (await _customerRepository.EmailExistsAsync(normalizedEmail, excludeCustomerId: existing.Id, cancellationToken: cancellationToken))
                    throw new InvalidOperationException($"A customer with email '{normalizedEmail}' already exists.");
                existing.Email = normalizedEmail;
                dirty = true;
            }

            // GetByPhoneNumberAsync is no-tracking, so Update is required to persist the fills.
            if (dirty)
            {
                _customerRepository.Update(existing);
                await _customerRepository.SaveChangesAsync(cancellationToken);
            }

            await _brandCustomerRepository.EnsureAsync(brandId.Value, existing.Id, BrandCustomerSource.Manual, createdBy, cancellationToken);
            return existing;
        }

        if (normalizedEmail != null && await _customerRepository.EmailExistsAsync(normalizedEmail, cancellationToken: cancellationToken))
            throw new InvalidOperationException($"A customer with email '{normalizedEmail}' already exists.");

        var customer = new Customer
        {
            PhoneNumber = normalizedPhone,
            FullName = fullName.Trim(),
            Email = normalizedEmail,
            Status = CustomerStatus.Active
        };

        await _customerRepository.AddAsync(customer, cancellationToken);
        await _customerRepository.SaveChangesAsync(cancellationToken);

        if (brandId.HasValue)
            await _brandCustomerRepository.EnsureAsync(brandId.Value, customer.Id, BrandCustomerSource.Manual, createdBy, cancellationToken);

        return customer;
    }

    /// <summary>
    /// Brand path (brandId set): fill-empty-only per CR-2026-09-06-11 — populate
    /// empty/placeholder fields, never overwrite existing values.
    /// Admin path (no brandId): full edit per CR-2026-09-07-14, with every changed
    /// field written to the audit log (email is the voucher delivery channel).
    /// </summary>
    public async Task<Customer> UpdateAsync(Guid id, string fullName, string? email, CancellationToken cancellationToken = default, Guid? brandId = null, Guid? changedBy = null)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));

        if (brandId.HasValue && await _brandCustomerRepository.FindAsync(brandId.Value, id, cancellationToken) == null)
            throw new KeyNotFoundException($"Customer with ID '{id}' was not found.");

        var customer = await _customerRepository.GetByIdAsync(id, cancellationToken);
        if (customer == null)
            throw new KeyNotFoundException($"Customer with ID '{id}' was not found.");

        var normalizedEmail = Customer.NormalizeEmail(email);

        if (brandId.HasValue)
        {
            if (customer.HasPlaceholderName())
                customer.FullName = fullName.Trim();

            if (normalizedEmail != null && string.IsNullOrWhiteSpace(customer.Email))
            {
                if (await _customerRepository.EmailExistsAsync(normalizedEmail, excludeCustomerId: id, cancellationToken: cancellationToken))
                    throw new InvalidOperationException($"A customer with email '{normalizedEmail}' already exists.");
                customer.Email = normalizedEmail;
            }

            await _customerRepository.SaveChangesAsync(cancellationToken);
            return customer;
        }

        if (normalizedEmail != null && !string.Equals(customer.Email, normalizedEmail, StringComparison.Ordinal)
            && await _customerRepository.EmailExistsAsync(normalizedEmail, excludeCustomerId: id, cancellationToken: cancellationToken))
            throw new InvalidOperationException($"A customer with email '{normalizedEmail}' already exists.");

        var trimmedName = fullName.Trim();
        if (customer.FullName != trimmedName)
            await AddAuditAsync(id, changedBy, nameof(Customer.FullName), customer.FullName, trimmedName, cancellationToken);
        if (customer.Email != normalizedEmail)
            await AddAuditAsync(id, changedBy, nameof(Customer.Email), customer.Email, normalizedEmail, cancellationToken);

        customer.FullName = trimmedName;
        customer.Email = normalizedEmail;

        await _customerRepository.SaveChangesAsync(cancellationToken);
        return customer;
    }

    private async Task AddAuditAsync(Guid customerId, Guid? changedBy, string field, string? oldValue, string? newValue, CancellationToken cancellationToken)
    {
        await _auditLogRepository.AddAsync(new CustomerAuditLog
        {
            CustomerId = customerId,
            ChangedByUserId = changedBy,
            Field = field,
            OldValue = oldValue,
            NewValue = newValue
        }, cancellationToken);
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
        string? search,
        CustomerStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default,
        Guid? brandId = null)
    {
        var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        var allItems = await _customerRepository.SearchAsync(term, status, cancellationToken, brandId);
        var totalCount = await _customerRepository.CountAsync(term, status, cancellationToken, brandId);

        var items = allItems
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default, Guid? brandId = null)
    {
        if (brandId.HasValue && await _brandCustomerRepository.FindAsync(brandId.Value, id, cancellationToken) == null)
            return null;

        return await _customerRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<bool> IsBlacklisted(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId, cancellationToken);
        return customer?.Status == CustomerStatus.Blacklisted;
    }

    /// <summary>Per-brand block (matrix S1): stops distribution/sale/transfer-in from this brand only. Never blocks redemption (P1).</summary>
    public async Task BlockAsync(Guid brandId, Guid customerId, CancellationToken cancellationToken = default)
    {
        await _brandCustomerRepository.SetBlockedAsync(brandId, customerId, blocked: true, cancellationToken);
    }

    /// <summary>Removes the per-brand block (matrix S1, P4 reversibility).</summary>
    public async Task UnblockAsync(Guid brandId, Guid customerId, CancellationToken cancellationToken = default)
    {
        await _brandCustomerRepository.SetBlockedAsync(brandId, customerId, blocked: false, cancellationToken);
    }

    public async Task<bool> IsBrandBlockedAsync(Guid brandId, Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _brandCustomerRepository.IsBlockedAsync(brandId, customerId, cancellationToken);
    }

    /// <summary>Mapping lookup for a single customer — used to fill IsBrandBlocked in API responses.</summary>
    public Task<BrandCustomer?> GetBrandMappingAsync(Guid brandId, Guid customerId, CancellationToken cancellationToken = default)
    {
        return _brandCustomerRepository.FindAsync(brandId, customerId, cancellationToken);
    }

    /// <summary>Mapping lookups for a page of customers — used to fill IsBrandBlocked in API list responses.</summary>
    public Task<IReadOnlyList<BrandCustomer>> GetBrandMappingsAsync(Guid brandId, IEnumerable<Guid> customerIds, CancellationToken cancellationToken = default)
    {
        return _brandCustomerRepository.GetForBrandAsync(brandId, customerIds, cancellationToken);
    }

    public async Task<CustomerImportResult> UpsertAsync(IEnumerable<CustomerImportRecord> records, CancellationToken cancellationToken = default, Guid? brandId = null, Guid? createdBy = null)
    {
        var created = 0;
        var updated = 0;
        var unchanged = 0;
        var errors = new List<CustomerImportError>();
        var skipped = new List<CustomerImportSkippedRow>();
        var batchPhones = new HashSet<string>(); // phones claimed by earlier rows of this import
        var batchEmails = new HashSet<string>(); // emails claimed by earlier rows of this import

        foreach (var record in records)
        {
            try
            {
                var normalizedPhone = Customer.NormalizePhoneNumber(record.PhoneNumber);
                if (string.IsNullOrEmpty(normalizedPhone))
                {
                    errors.Add(Error(record, "Invalid phone number."));
                    continue;
                }

                if (!batchPhones.Add(normalizedPhone))
                {
                    errors.Add(Error(record, "Duplicate phone number within the import file."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(record.FullName))
                {
                    errors.Add(Error(record, "Full name is required."));
                    continue;
                }

                var normalizedEmail = Customer.NormalizeEmail(record.Email);
                var customer = await _customerRepository.GetByPhoneNumberAsync(normalizedPhone, cancellationToken);

                if (normalizedEmail != null)
                {
                    // Fill-empty-only: the incoming email is only written onto a record
                    // whose email is empty — the uniqueness check only matters then.
                    var wouldWriteEmail = customer == null || string.IsNullOrWhiteSpace(customer.Email);
                    if (wouldWriteEmail)
                    {
                        var takenElsewhere = await _customerRepository.EmailExistsAsync(
                            normalizedEmail,
                            excludeCustomerId: customer?.Id,
                            cancellationToken: cancellationToken);

                        if (takenElsewhere)
                        {
                            errors.Add(Error(record, $"Email '{normalizedEmail}' is already used by another customer."));
                            continue;
                        }

                        if (!batchEmails.Add(normalizedEmail))
                        {
                            errors.Add(Error(record, $"Email '{normalizedEmail}' appears more than once in the import file."));
                            continue;
                        }
                    }
                }

                if (customer != null)
                {
                    // CR-2026-09-06-11 fill-empty-only: populate empty/placeholder fields,
                    // never overwrite existing values; rows with rejected (kept) values are
                    // reported so the brand knows its file did not win.
                    // CR-2026-09-06-10: GetByPhoneNumberAsync is no-tracking — Update is
                    // required or the fills are silently never persisted.
                    var filled = false;
                    var keptFields = new List<string>();

                    var incomingName = record.FullName.Trim();
                    if (customer.HasPlaceholderName())
                    {
                        customer.FullName = incomingName;
                        filled = true;
                    }
                    else if (!string.Equals(customer.FullName, incomingName, StringComparison.Ordinal))
                    {
                        keptFields.Add("name");
                    }

                    if (normalizedEmail != null)
                    {
                        if (string.IsNullOrWhiteSpace(customer.Email))
                        {
                            customer.Email = normalizedEmail;
                            filled = true;
                        }
                        else if (!string.Equals(customer.Email, normalizedEmail, StringComparison.Ordinal))
                        {
                            keptFields.Add("email");
                        }
                    }

                    if (filled)
                    {
                        _customerRepository.Update(customer);
                        updated++;
                    }
                    else if (keptFields.Count == 0)
                    {
                        unchanged++;
                    }

                    if (keptFields.Count > 0)
                    {
                        skipped.Add(new CustomerImportSkippedRow(record.Row, record.PhoneNumber, record.FullName, record.Email,
                            $"Kept existing {string.Join(" and ", keptFields)} — brand imports cannot overwrite existing values."));
                    }

                    if (brandId.HasValue)
                        await _brandCustomerRepository.EnsureAsync(brandId.Value, customer.Id, BrandCustomerSource.Import, createdBy, cancellationToken);
                }
                else
                {
                    var newCustomer = new Customer
                    {
                        PhoneNumber = normalizedPhone,
                        FullName = record.FullName.Trim(),
                        Email = normalizedEmail,
                        Status = CustomerStatus.Active
                    };
                    await _customerRepository.AddAsync(newCustomer, cancellationToken);
                    if (brandId.HasValue)
                        await _brandCustomerRepository.EnsureAsync(brandId.Value, newCustomer.Id, BrandCustomerSource.Import, createdBy, cancellationToken);
                    created++;
                }
            }
            catch (Exception ex)
            {
                errors.Add(Error(record, ex.Message));
            }
        }

        await _customerRepository.SaveChangesAsync(cancellationToken);

        return new CustomerImportResult(created, updated, unchanged, errors, skipped);
    }

    private static CustomerImportError Error(CustomerImportRecord record, string message)
        => new(record.Row, record.PhoneNumber, record.FullName, record.Email, message);
}

public record CustomerImportRecord(string PhoneNumber, string FullName, string? Email, int Row = 0);

/// <summary>
/// A failed import row: the full source data plus the reason, so it can be
/// exported as an error log (CSV) for offline fixing.
/// </summary>
public record CustomerImportError(int Row, string PhoneNumber, string FullName, string? Email, string Message);

/// <summary>
/// An import row whose incoming values were NOT written because brand writes are
/// fill-empty-only (CR-2026-09-06-11): the differing existing values were kept.
/// </summary>
public record CustomerImportSkippedRow(int Row, string PhoneNumber, string FullName, string? Email, string Message);

public record CustomerImportResult(int Created, int Updated, int Unchanged, IReadOnlyList<CustomerImportError> Errors, IReadOnlyList<CustomerImportSkippedRow> Skipped);
