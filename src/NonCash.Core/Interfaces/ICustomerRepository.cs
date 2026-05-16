using NonCash.Core.Entities;

namespace NonCash.Core.Interfaces;

public interface ICustomerRepository : IRepository<Customer>
{
    Task<Customer?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);
    Task<bool> PhoneNumberExistsAsync(string phoneNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<Customer>> SearchAsync(string? phoneNumber, string? name, string? email, CustomerStatus? status, CancellationToken cancellationToken = default);
    Task<int> CountAsync(string? phoneNumber, string? name, string? email, CustomerStatus? status, CancellationToken cancellationToken = default);
}
