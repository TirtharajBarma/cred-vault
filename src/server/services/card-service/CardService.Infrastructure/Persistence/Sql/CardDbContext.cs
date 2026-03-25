using CardService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CardService.Infrastructure.Persistence.Sql;

public sealed class CardDbContext(DbContextOptions<CardDbContext> options) : DbContext(options)
{
    public DbSet<CreditCard> CreditCards => Set<CreditCard>();
    public DbSet<CardIssuer> CardIssuers => Set<CardIssuer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CardIssuer>(entity =>
        {
            entity.ToTable("CardIssuers");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Network).IsRequired().HasConversion<int>();
            entity.Property(x => x.IsActive).IsRequired();

            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();

            entity.HasIndex(x => x.Network).IsUnique();
        });

        modelBuilder.Entity<CreditCard>(entity =>
        {
            entity.ToTable("CreditCards");
            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.Issuer)
                .WithMany()
                .HasForeignKey(x => x.IssuerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(x => x.CardholderName).IsRequired().HasMaxLength(256);
            entity.Property(x => x.Last4).IsRequired().HasMaxLength(4);
            entity.Property(x => x.MaskedNumber).IsRequired().HasMaxLength(32);

            entity.Property(x => x.ExpMonth).IsRequired();
            entity.Property(x => x.ExpYear).IsRequired();

            entity.Property(x => x.CreditLimit).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(x => x.OutstandingBalance).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(x => x.BillingCycleStartDay).IsRequired();

            entity.Property(x => x.IsDefault).IsRequired();
            entity.Property(x => x.IsVerified).IsRequired();
            entity.Property(x => x.VerifiedAtUtc);

            entity.Property(x => x.IsDeleted).IsRequired();
            entity.Property(x => x.DeletedAtUtc);

            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();

            entity.HasQueryFilter(x => !x.IsDeleted);

            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => new { x.UserId, x.IsDefault });
            entity.HasIndex(x => x.IssuerId);
        });
    }
}
