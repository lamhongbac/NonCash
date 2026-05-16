using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;

namespace NonCash.Infrastructure.Services;

public class CsvCustomerImportService : ICustomerImportService
{
    private readonly CustomerService _customerService;

    public CsvCustomerImportService(CustomerService customerService)
    {
        _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
    }

    public async Task<CustomerImportResult> ImportFromCsvAsync(Stream csvStream, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(csvStream);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
            BadDataFound = null
        };
        using var csv = new CsvReader(reader, config);

        var records = csv.GetRecords<CsvCustomerRecord>().ToList();

        var importRecords = records
            .Where(r => !string.IsNullOrWhiteSpace(r.PhoneNumber))
            .Select(r => new CustomerImportRecord(r.PhoneNumber, r.FullName, r.Email))
            .ToList();

        return await _customerService.UpsertAsync(importRecords, cancellationToken);
    }

    private class CsvCustomerRecord
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
    }
}
