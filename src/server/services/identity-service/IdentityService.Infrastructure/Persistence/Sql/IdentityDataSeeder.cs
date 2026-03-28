using IdentityService.Domain.Entities;
using IdentityService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence.Sql;

public static class IdentityDataSeeder
{
    public static async Task SeedAsync(IdentityDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (await dbContext.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var users = new List<IdentityUser>
        {
            new()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Email = "admin@credvault.dev",
                FullName = "Admin User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                IsEmailVerified = true,
                Status = UserStatus.Active,
                Role = UserRole.Admin,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Email = "user@credvault.dev",
                FullName = "Normal User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("User@123"),
                IsEmailVerified = true,
                Status = UserStatus.Active,
                Role = UserRole.User,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            }
        };

        await dbContext.Users.AddRangeAsync(users, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
