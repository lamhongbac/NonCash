using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface ICustomerRepository : IRepository<Customer>
{
    Task<Customer?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);
    Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> PhoneNumberExistsAsync(string phoneNumber, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, Guid? excludeCustomerId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<Customer>> SearchAsync(string? search, CustomerStatus? status, CancellationToken cancellationToken = default, Guid? brandId = null);
    Task<int> CountAsync(string? search, CustomerStatus? status, CancellationToken cancellationToken = default, Guid? brandId = null);
}
