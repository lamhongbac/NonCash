using NonCash.Core.Services;

namespace NonCash.Core.Interfaces;

public interface ICustomerImportService
{
    Task<CustomerImportResult> ImportFromCsvAsync(Stream csvStream, CancellationToken cancellationToken = default);
}
