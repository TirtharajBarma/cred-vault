using CardService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CardService.Infrastructure.Persistence.Sql;

public sealed class CardDbContext(DbContextOptions<CardDbContext> options) : DbContext(options)     // options -> DB connection config
{
    // DbSet -> table
    // entity -> row
    public DbSet<CreditCard> CreditCards => Set<CreditCard>();
    public DbSet<CardIssuer> CardIssuers => Set<CardIssuer>();
    public DbSet<CardTransaction> CardTransactions => Set<CardTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)      // how DB table should be structured
    {
        modelBuilder.Entity<CardIssuer>(entity =>
        {
            entity.ToTable("CardIssuers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Network).IsRequired().HasConversion<int>();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
            entity.HasIndex(x => x.Network);
        });

        modelBuilder.Entity<CreditCard>(entity =>
        {
            entity.ToTable("CreditCards");
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Issuer)        // -> credit card depends on card issuer
                .WithMany()                        // -> one Issuer have many credit-cards
                .HasForeignKey(x => x.IssuerId)     // use IssuerId in CreditCard as FK : [CreditCards.IssuerId  →  CardIssuers.Id]
                // HasPrincipalKey(x => x.IssuerCode); [CreditCards.IssuerId → CardIssuers.IssuerCode]
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.CardholderName).IsRequired().HasMaxLength(256);
            entity.Property(x => x.Last4).IsRequired().HasMaxLength(4);
            entity.Property(x => x.MaskedNumber).IsRequired().HasMaxLength(32);
            entity.Property(x => x.EncryptedCardNumber).HasMaxLength(512);
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

        modelBuilder.Entity<CardTransaction>(entity =>
        {
            entity.ToTable("CardTransactions");
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Card)                  // transaction depends on credit card
                .WithMany()
                .HasForeignKey(x => x.CardId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.CardId).IsRequired();
            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.Type).IsRequired().HasConversion<int>();
            entity.Property(x => x.Amount).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(x => x.Description).IsRequired().HasMaxLength(256);
            entity.Property(x => x.DateUtc).IsRequired();
            entity.HasIndex(x => x.CardId);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.DateUtc);
            entity.HasQueryFilter(x => !x.Card!.IsDeleted);     // only return transactions where the related card is NOT deleted
        });
    }
}
