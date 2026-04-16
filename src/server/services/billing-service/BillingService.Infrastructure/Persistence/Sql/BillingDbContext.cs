using BillingService.Domain.Entities;
using Shared.Contracts.Enums;
using Microsoft.EntityFrameworkCore;

namespace BillingService.Infrastructure.Persistence.Sql;

public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options) : DbContext(options)
{
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<RewardTier> RewardTiers => Set<RewardTier>();
    public DbSet<RewardAccount> RewardAccounts => Set<RewardAccount>();
    public DbSet<RewardTransaction> RewardTransactions => Set<RewardTransaction>();
    public DbSet<Statement> Statements => Set<Statement>();
    public DbSet<StatementTransaction> StatementTransactions => Set<StatementTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Bill>(entity =>
        {
            entity.ToTable("Bills");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.CardId).IsRequired();
            entity.Property(x => x.CardNetwork).IsRequired().HasConversion<int>();
            entity.Property(x => x.IssuerId).IsRequired();

            entity.Property(x => x.Amount).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(x => x.MinDue).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(x => x.Currency).IsRequired().HasMaxLength(8);

            entity.Property(x => x.BillingDateUtc).IsRequired();
            entity.Property(x => x.DueDateUtc).IsRequired();

            entity.Property(x => x.Status).IsRequired().HasConversion<int>();

            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();

            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => new { x.UserId, x.Status });
            entity.HasIndex(x => x.DueDateUtc);
        });

        modelBuilder.Entity<RewardTier>(entity =>
        {
            entity.ToTable("RewardTiers");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.CardNetwork).IsRequired().HasConversion<int>();
            entity.Property(x => x.IssuerId);
            entity.Property(x => x.MinSpend).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(x => x.RewardRate).IsRequired().HasColumnType("decimal(9,6)");

            entity.Property(x => x.EffectiveFromUtc).IsRequired();
            entity.Property(x => x.EffectiveToUtc);

            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();

            entity.HasIndex(x => new { x.CardNetwork, x.EffectiveFromUtc });
        });

        modelBuilder.Entity<RewardAccount>(entity =>
        {
            entity.ToTable("RewardAccounts");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.RewardTierId).IsRequired(false);

            entity.Property(x => x.PointsBalance).IsRequired().HasColumnType("decimal(18,2)");

            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();

            entity.HasOne(x => x.RewardTier)
                .WithMany()
                .HasForeignKey(x => x.RewardTierId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.UserId).IsUnique();
        });

        modelBuilder.Entity<RewardTransaction>(entity =>
        {
            entity.ToTable("RewardTransactions");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.RewardAccountId).IsRequired();
            entity.Property(x => x.BillId).IsRequired(false);
            entity.Property(x => x.Points).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(x => x.Type).IsRequired().HasConversion<int>();
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasOne<RewardAccount>()
                .WithMany()
                .HasForeignKey(x => x.RewardAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Bill>()
                .WithMany()
                .HasForeignKey(x => x.BillId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.RewardAccountId);
            entity.HasIndex(x => x.BillId);
            entity.HasIndex(x => x.CreatedAtUtc);
        });

        modelBuilder.Entity<Statement>(entity =>
        {
            entity.ToTable("Statements");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.CardId).IsRequired();
            entity.Property(x => x.StatementPeriod).IsRequired().HasMaxLength(100);
            entity.Property(x => x.CardLast4).IsRequired().HasMaxLength(10);
            entity.Property(x => x.CardNetwork).IsRequired().HasMaxLength(50);
            entity.Property(x => x.IssuerName).IsRequired().HasMaxLength(100);

            entity.Property(x => x.OpeningBalance).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(x => x.TotalPurchases).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(x => x.TotalPayments).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(x => x.TotalRefunds).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(x => x.PenaltyCharges).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(x => x.InterestCharges).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(x => x.ClosingBalance).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(x => x.MinimumDue).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(x => x.AmountPaid).HasColumnType("decimal(18,2)");
            entity.Property(x => x.CreditLimit).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(x => x.AvailableCredit).IsRequired().HasColumnType("decimal(18,2)");

            entity.Property(x => x.Status).IsRequired().HasConversion<int>();

            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();

            entity.HasOne(x => x.Bill)
                .WithMany(b => b.Statements)
                .HasForeignKey(x => x.BillId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.CardId);
            entity.HasIndex(x => new { x.UserId, x.Status });
            entity.HasIndex(x => x.PeriodEndUtc);
        });

        modelBuilder.Entity<StatementTransaction>(entity =>
        {
            entity.ToTable("StatementTransactions");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.StatementId).IsRequired();
            entity.Property(x => x.Type).IsRequired().HasMaxLength(50);
            entity.Property(x => x.Amount).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(x => x.Description).IsRequired().HasMaxLength(256);
            entity.Property(x => x.DateUtc).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasOne(x => x.Statement)
                .WithMany(s => s.Transactions)
                .HasForeignKey(x => x.StatementId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_StatementTransactions_Statements_StatementId");

            entity.HasIndex(x => x.StatementId);
            entity.HasIndex(x => x.DateUtc);
        });
    }
}
