using IdentityService.Domain.Entities;
using IdentityService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence.Sql;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<IdentityUser> Users => Set<IdentityUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdentityUser>(entity =>
        {
            entity.ToTable("identity_users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).IsRequired().HasMaxLength(256);
            entity.Property(x => x.FullName).IsRequired().HasMaxLength(256);
            entity.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(x => x.EmailVerificationOtp).HasMaxLength(16);
            entity.Property(x => x.PasswordResetOtp).HasMaxLength(16);
            entity.Property(x => x.Status)
                .HasConversion(value => StatusToDb(value), value => DbToStatus(value))
                .IsRequired()
                .HasMaxLength(64);
            entity.Property(x => x.Role)
                .HasConversion(value => RoleToDb(value), value => DbToRole(value))
                .IsRequired()
                .HasMaxLength(64);
            entity.HasIndex(x => x.Email).IsUnique();
        });
    }

    private static string StatusToDb(UserStatus status)
    {
        return status switch
        {
            UserStatus.Active => "active",
            UserStatus.Suspended => "suspended",
            UserStatus.Blocked => "blocked",
            _ => "pending-verification"
        };
    }

    private static UserStatus DbToStatus(string value)
    {
        return value switch
        {
            "active" => UserStatus.Active,
            "suspended" => UserStatus.Suspended,
            "blocked" => UserStatus.Blocked,
            _ => UserStatus.PendingVerification
        };
    }

    private static string RoleToDb(UserRole role)
    {
        return role == UserRole.Admin ? "admin" : "user";
    }

    private static UserRole DbToRole(string value)
    {
        return value == "admin" ? UserRole.Admin : UserRole.User;
    }
}
