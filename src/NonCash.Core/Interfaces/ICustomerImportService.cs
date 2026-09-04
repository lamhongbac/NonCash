using NonCash.Core.Services;

namespace NonCash.Core.Interfaces;

public interface ICustomerImportService
{
    /// <summary>
    /// Imports customers from an uploaded file. Accepts Excel (.xlsx) and CSV
    /// (comma, semicolon or tab separated, header optional). When <paramref name="brandId"/>
    /// is set, every imported (or updated) customer is mapped to that brand.
    /// </summary>
    Task<CustomerImportResult> ImportAsync(Stream fileStream, CancellationToken cancellationToken = default, Guid? brandId = null, Guid? createdBy = null);
}
