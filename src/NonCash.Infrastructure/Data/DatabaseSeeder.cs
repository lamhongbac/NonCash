using Microsoft.Extensions.DependencyInjection;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;

namespace NonCash.Infrastructure.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAdminAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var adminExists = await userRepository.UsernameExistsAsync("admin", CancellationToken.None);
        if (!adminExists)
        {
            var admin = new UserAccount
            {
                Username = "admin",
                PasswordHash = authService.HashPassword("Admin@123"),
                FullName = "System Administrator",
                Role = UserRole.Admin,
                BrandId = null,
                Status = UserStatus.Active
            };

            await userRepository.AddAsync(admin, CancellationToken.None);
            await userRepository.SaveChangesAsync(CancellationToken.None);
        }
    }
}
