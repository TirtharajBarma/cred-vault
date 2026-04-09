using PaymentService.Domain.Entities;
using PaymentService.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using MassTransit;

namespace PaymentService.Infrastructure.Persistence.Sql;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<PaymentOrchestrationSagaState> PaymentOrchestrationSagas => Set<PaymentOrchestrationSagaState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.CardId).IsRequired();
            entity.Property(x => x.BillId).IsRequired();
            entity.Property(x => x.Amount).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(x => x.PaymentType).IsRequired().HasConversion<int>();
            entity.Property(x => x.Status).IsRequired().HasConversion<int>();
            entity.Property(x => x.FailureReason).HasMaxLength(500);

            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();

            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.BillId);
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("Transactions");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Amount).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(x => x.Type).IsRequired().HasConversion<int>();
            entity.Property(x => x.Description).HasMaxLength(250);
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasOne(x => x.Payment)
                .WithMany(x => x.Transactions)
                .HasForeignKey(x => x.PaymentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.PaymentId);
        });

        // Orchestration Saga Configuration
        modelBuilder.Entity<PaymentOrchestrationSagaState>(entity =>
        {
            entity.ToTable("PaymentOrchestrationSagas");
            entity.HasKey(x => x.CorrelationId);

            entity.Property(x => x.CurrentState).HasMaxLength(64);
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.FullName).HasMaxLength(256);
            entity.Property(x => x.PaymentType).HasMaxLength(32);
            entity.Property(x => x.CompensationReason).HasMaxLength(500);
            entity.Property(x => x.PaymentError).HasMaxLength(500);
            entity.Property(x => x.BillUpdateError).HasMaxLength(500);
            entity.Property(x => x.CardDeductionError).HasMaxLength(500);
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.RewardsAmount).HasColumnType("decimal(18,2)");
        });
    }
}
