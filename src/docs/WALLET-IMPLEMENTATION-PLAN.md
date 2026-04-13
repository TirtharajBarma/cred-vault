# Wallet + CardDeduction Implementation Plan

**Date:** 2026-04-13
**Status:** Planning (DO NOT IMPLEMENT YET)

---

## Overview

This plan adds a **Wallet System** to track demo money flow, making payment rollbacks visible and meaningful.

### Key Components
- **Wallet** - User's virtual bank account (unlimited, user tops up)
- **CardDeduction** - Reduces OutstandingBalance (credit card debt)
- **Both work together** - Wallet pays, Card debt clears

---

## Current State vs Proposed State

### Current (Confusing)
```
Wallet: ❌ DOESN'T EXIST
Card OutstandingBalance: ₹10,000 (reduces on payment)
Bill: ₹10,000 (marks as paid)

Problem: NO MONEY TRACKING
Rollback: INVISIBLE (user doesn't see money return)
```

### Proposed (Clear)
```
Wallet: ₹10,000 (NEW - user tops up)
Card OutstandingBalance: ₹10,000 (reduces on payment)
Bill: ₹10,000 (marks as paid)

Solution: MONEY IS VISIBLE
Rollback: VISIBLE (user sees money refund to wallet)
```

---

## Implementation Summary

| Item | Where | Complexity |
|------|-------|------------|
| Database Tables | PaymentService (credvault_payments) | Low |
| New Events | shared.contracts | Low |
| Wallet Service | Add to PaymentService | Low |
| API Endpoints | Add to PaymentService | Low |
| Modify Payment Flow | InitiatePaymentCommandHandler | Low |
| Modify Saga | PaymentOrchestrationSaga | Low |
| Gateway Routes | ocelot.json | Low |

---

## 1. Database Changes

### Location: `credvault_payments` (PaymentService)

### New Tables

```sql
-- Table 1: UserWallets
CREATE TABLE UserWallets (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL,
    Balance DECIMAL(18,2) NOT NULL DEFAULT 0,
    TotalTopUps DECIMAL(18,2) NOT NULL DEFAULT 0,
    TotalSpent DECIMAL(18,2) NOT NULL DEFAULT 0,
    CreatedAtUtc DATETIME2 NOT NULL,
    UpdatedAtUtc DATETIME2 NOT NULL
);

-- Table 2: WalletTransactions
CREATE TABLE WalletTransactions (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    WalletId UNIQUEIDENTIFIER NOT NULL,
    Amount DECIMAL(18,2) NOT NULL, -- positive = credit, negative = debit
    Type INT NOT NULL, -- 0=TopUp, 1=Payment, 2=Refund
    ReferenceId UNIQUEIDENTIFIER NULL, -- PaymentId, BillId, etc.
    Description NVARCHAR(256) NOT NULL,
    CreatedAtUtc DATETIME2 NOT NULL,
    CONSTRAINT FK_WalletTransactions_UserWallets 
        FOREIGN KEY (WalletId) REFERENCES UserWallets(Id)
);

-- Indexes
CREATE INDEX IX_UserWallets_UserId ON UserWallets(UserId);
CREATE INDEX IX_WalletTransactions_WalletId ON WalletTransactions(WalletId);
```

### New Enums

```csharp
// File: PaymentService.Domain/Enums/WalletTransactionType.cs
public enum WalletTransactionType
{
    TopUp = 0,
    Payment = 1,
    Refund = 2,
    Adjustment = 3
}
```

---

## 2. New Entities

### File: `PaymentService.Domain/Entities/UserWallet.cs`
```csharp
public class UserWallet
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public decimal Balance { get; set; }
    public decimal TotalTopUps { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    
    public ICollection<WalletTransaction> Transactions { get; set; }
}
```

### File: `PaymentService.Domain/Entities/WalletTransaction.cs`
```csharp
public class WalletTransaction
{
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }
    public UserWallet? Wallet { get; set; }
    public decimal Amount { get; set; }
    public WalletTransactionType Type { get; set; }
    public Guid? ReferenceId { get; set; }
    public string Description { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
```

---

## 3. New Events

### File: `shared.contracts/Shared.Contracts/Events/Wallet/WalletEvents.cs`

```csharp
namespace Shared.Contracts.Events.Wallet;

public interface IWalletDeducted
{
    Guid CorrelationId { get; }
    Guid PaymentId { get; }
    Guid UserId { get; }
    decimal Amount { get; }
    DateTime DeductedAt { get; }
}

public interface IWalletRefundRequested
{
    Guid CorrelationId { get; }
    Guid PaymentId { get; }
    Guid UserId { get; }
    decimal Amount { get; }
    string Reason { get; }
    DateTime RequestedAt { get; }
}

public interface IWalletRefunded
{
    Guid CorrelationId { get; }
    Guid UserId { get; }
    decimal Amount { get; }
    DateTime RefundedAt { get; }
}
```

---

## 4. Database Context Changes

### File: `PaymentService.Infrastructure/Persistence/Sql/PaymentDbContext.cs`

Add to `OnModelCreating`:

```csharp
modelBuilder.Entity<UserWallet>(entity =>
{
    entity.ToTable("UserWallets");
    entity.HasKey(x => x.Id);
    entity.Property(x => x.Balance).IsRequired().HasColumnType("decimal(18,2)");
    entity.Property(x => x.TotalTopUps).IsRequired().HasColumnType("decimal(18,2)");
    entity.Property(x => x.TotalSpent).IsRequired().HasColumnType("decimal(18,2)");
    entity.Property(x => x.CreatedAtUtc).IsRequired();
    entity.Property(x => x.UpdatedAtUtc).IsRequired();
    entity.HasIndex(x => x.UserId).IsUnique();
});

modelBuilder.Entity<WalletTransaction>(entity =>
{
    entity.ToTable("WalletTransactions");
    entity.HasKey(x => x.Id);
    entity.Property(x => x.Amount).IsRequired().HasColumnType("decimal(18,2)");
    entity.Property(x => x.Type).IsRequired().HasConversion<int>();
    entity.Property(x => x.Description).IsRequired().HasMaxLength(256);
    entity.Property(x => x.CreatedAtUtc).IsRequired();
    
    entity.HasOne(x => x.Wallet)
        .WithMany(w => w.Transactions)
        .HasForeignKey(x => x.WalletId)
        .OnDelete(DeleteBehavior.Cascade);
    
    entity.HasIndex(x => x.WalletId);
});
```

### Add to DbSet
```csharp
public DbSet<UserWallet> UserWallets => Set<UserWallet>();
public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
```

---

## 5. Repository

### File: `PaymentService.Application/Interfaces/IWalletRepository.cs`
```csharp
public interface IWalletRepository
{
    Task<UserWallet?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<UserWallet?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserWallet> CreateAsync(UserWallet wallet, CancellationToken ct = default);
    Task UpdateAsync(UserWallet wallet, CancellationToken ct = default);
    Task AddTransactionAsync(WalletTransaction transaction, CancellationToken ct = default);
    Task<List<WalletTransaction>> GetTransactionsAsync(Guid walletId, int page, int pageSize, CancellationToken ct = default);
}
```

### File: `PaymentService.Infrastructure/Persistence/Repositories/SqlWalletRepository.cs`
```csharp
// Implementation of IWalletRepository using EF Core
```

---

## 6. Service

### File: `PaymentService.Application/Services/IWalletService.cs`
```csharp
public interface IWalletService
{
    Task<UserWallet> GetOrCreateWalletAsync(Guid userId, CancellationToken ct = default);
    Task<UserWallet> TopUpAsync(Guid userId, decimal amount, string description, CancellationToken ct = default);
    Task<(bool Success, string? Error)> DeductAsync(Guid userId, decimal amount, Guid paymentId, CancellationToken ct = default);
    Task<bool> RefundAsync(Guid userId, decimal amount, Guid paymentId, string reason, CancellationToken ct = default);
    Task<decimal> GetBalanceAsync(Guid userId, CancellationToken ct = default);
}
```

### File: `PaymentService.Application/Services/WalletService.cs`
```csharp
public class WalletService : IWalletService
{
    // Implementation with:
    // - GetOrCreateWalletAsync: Auto-create if not exists
    // - TopUpAsync: Add money to wallet
    // - DeductAsync: Deduct money (fails if insufficient)
    // - RefundAsync: Refund money back
    // - GetBalanceAsync: Get current balance
}
```

---

## 7. API Endpoints

### File: `PaymentService.Application/Controllers/WalletsController.cs` (or add to PaymentsController)

```csharp
[ApiController]
[Route("api/v1/wallets")]
public class WalletsController : ControllerBase
{
    [HttpPost("topup")]
    public async Task<IActionResult> TopUp([FromBody] TopUpRequest request);
    
    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance();
    
    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions([FromQuery] int page = 1, [FromQuery] int pageSize = 20);
    
    [HttpGet("me")]
    public async Task<IActionResult> GetMyWallet();
}
```

---

## 8. New Commands/Queries

### File: `PaymentService.Application/Commands/Wallets/TopUpWalletCommand.cs`
```csharp
public record TopUpWalletCommand(Guid UserId, decimal Amount, string Description) : IRequest<Result>;
```

### File: `PaymentService.Application/Queries/Wallets/GetWalletBalanceQuery.cs`
```csharp
public record GetWalletBalanceQuery(Guid UserId) : IRequest<decimal>;
```

---

## 9. Modify Payment Flow

### File: `PaymentService.Application/Commands/Payments/InitiatePaymentCommandHandler.cs`

**ADD at start of Handle method:**
```csharp
// 1. Check wallet balance
var walletBalance = await _walletService.GetBalanceAsync(request.UserId, ct);
if (walletBalance < request.Amount)
{
    return Result.Failure($"Insufficient wallet balance. Required: ₹{request.Amount:N2}, Available: ₹{walletBalance:N2}");
}

// 2. Deduct from wallet
var deductResult = await _walletService.DeductAsync(request.UserId, request.Amount, payment.Id, ct);
if (!deductResult.Success)
{
    return Result.Failure(deductResult.Error);
}
```

**ADD to payload published to saga:**
```csharp
new { /* existing fields */, WalletDeducted = true }
```

---

## 10. Modify Saga Events

### File: `shared.contracts/Shared.Contracts/Events/Saga/SagaOrchestrationEvents.cs`

**ADD to IStartPaymentOrchestration:**
```csharp
public interface IStartPaymentOrchestration
{
    // existing fields...
    decimal RewardsAmount { get; }
    bool WalletDeducted { get; } // NEW
    DateTime StartedAt { get; }
}
```

**ADD to PaymentOrchestrationSagaState:**
```csharp
public bool WalletDeducted { get; set; }
```

---

## 11. Modify Saga - Add Refund on Rollback

### File: `PaymentService.Application/Sagas/PaymentOrchestrationSaga.cs`

**ADD new state variable:**
```csharp
During(AwaitingBillUpdate,
    When(IBillUpdateSucceeded)
        .Then(context =>
        {
            context.Saga.WalletDeducted = context.Message.DeductedAt != default;
        })
);
```

**ADD to Compensating state - REFUND WALLET ON ANY FAILURE:**
```csharp
During(Compensating,
    // Existing revert logic...
    
    // NEW: Refund wallet if it was deducted
    WhenEnter(BehaviorAsync(async context =>
    {
        if (context.Saga.WalletDeducted)
        {
            await context.Publish(new IWalletRefundRequested
            {
                CorrelationId = context.Saga.CorrelationId,
                PaymentId = context.Saga.PaymentId,
                UserId = context.Saga.UserId,
                Amount = context.Saga.Amount,
                Reason = context.Saga.CompensationReason ?? "Payment failed",
                RequestedAt = DateTime.UtcNow
            });
        }
    }))
);
```

---

## 12. New Consumer - Wallet Refund

### File: `PaymentService.Application/Sagas/Consumers/WalletRefundConsumer.cs`
```csharp
public class WalletRefundConsumer : IConsumer<IWalletRefundRequested>
{
    public async Task Consume(ConsumeContext<IWalletRefundRequested> context)
    {
        var result = await _walletService.RefundAsync(
            context.Message.UserId,
            context.Message.Amount,
            context.Message.PaymentId,
            context.Message.Reason
        );
        
        await context.Publish(new IWalletRefunded
        {
            CorrelationId = context.Message.CorrelationId,
            UserId = context.Message.UserId,
            Amount = context.Message.Amount,
            RefundedAt = DateTime.UtcNow
        });
    }
}
```

---

## 13. MassTransit Configuration

### File: `PaymentService.API/Program.cs`

**ADD consumer registration:**
```csharp
x.AddConsumer<WalletRefundConsumer>();

// ADD to payment-orchestration or create new queue
cfg.ReceiveEndpoint("wallet-refund", e =>
{
    e.ConfigureConsumer<WalletRefundConsumer>(ctx);
    e.Bind<IWalletRefundRequested>();
});
```

---

## 14. Gateway Routes

### File: `src/server/gateway/ocelot.json`

**ADD routes:**
```json
{
    "UpstreamPathTemplate": "/api/v1/wallets/me",
    "UpstreamHttpMethod": ["GET"],
    "DownstreamPathTemplate": "/api/v1/wallets/me",
    "DownstreamScheme": "http",
    "DownstreamHostAndPorts": [{ "Host": "localhost", "Port": 5004 }]
},
{
    "UpstreamPathTemplate": "/api/v1/wallets/topup",
    "UpstreamHttpMethod": ["POST"],
    "DownstreamPathTemplate": "/api/v1/wallets/topup",
    "DownstreamScheme": "http",
    "DownstreamHostAndPorts": [{ "Host": "localhost", "Port": 5004 }]
},
{
    "UpstreamPathTemplate": "/api/v1/wallets/balance",
    "UpstreamHttpMethod": ["GET"],
    "DownstreamPathTemplate": "/api/v1/wallets/balance",
    "DownstreamScheme": "http",
    "DownstreamHostAndPorts": [{ "Host": "localhost", "Port": 5004 }]
},
{
    "UpstreamPathTemplate": "/api/v1/wallets/transactions",
    "UpstreamHttpMethod": ["GET"],
    "DownstreamPathTemplate": "/api/v1/wallets/transactions",
    "DownstreamScheme": "http",
    "DownstreamHostAndPorts": [{ "Host": "localhost", "Port": 5004 }]
}
```

---

## 15. Docker Compose

### File: `docker-compose.yml`

**NO CHANGES NEEDED** - Using existing PaymentService database

---

## Complete File List

### New Files to Create

```
NEW FILES:
│
├── PaymentService/
│   ├── PaymentService.Domain/
│   │   └── Entities/
│   │       ├── UserWallet.cs
│   │       └── WalletTransaction.cs
│   │   └── Enums/
│   │       └── WalletTransactionType.cs
│   │
│   ├── PaymentService.Application/
│   │   ├── Services/
│   │   │   ├── IWalletService.cs
│   │   │   └── WalletService.cs
│   │   ├── Commands/Wallets/
│   │   │   └── TopUpWalletCommand.cs
│   │   └── Queries/Wallets/
│   │       ├── GetWalletBalanceQuery.cs
│   │       └── GetWalletTransactionsQuery.cs
│   │
│   ├── PaymentService.Infrastructure/
│   │   ├── Persistence/Repositories/
│   │   │   └── SqlWalletRepository.cs
│   │   └── Services/
│   │       └── WalletRefundConsumer.cs (or add to existing Consumers)
│   │
│   └── PaymentService.API/
│       └── Controllers/
│           └── WalletsController.cs
│
└── shared.contracts/
    └── Shared.Contracts/
        └── Events/
            └── Wallet/
                └── WalletEvents.cs
```

### Files to Modify

```
MODIFY FILES:
│
├── PaymentService/
│   ├── PaymentService.Application/
│   │   └── Commands/Payments/
│   │       └── InitiatePaymentCommandHandler.cs
│   │           → ADD: Wallet check and deduct
│   │
│   ├── PaymentService.Application/
│   │   └── Sagas/
│   │       └── PaymentOrchestrationSaga.cs
│   │           → ADD: Wallet refund on rollback
│   │
│   ├── PaymentService.Infrastructure/
│   │   └── Persistence/Sql/
│   │       └── PaymentDbContext.cs
│   │           → ADD: UserWallet, WalletTransaction DbSets
│   │
│   ├── PaymentService.API/
│   │   └── Program.cs
│   │       → ADD: WalletRefundConsumer registration
│   │
│   └── PaymentService.Infrastructure/
│       └── Messaging/Consumers/
│           └── PaymentConsumers.cs (or create new file)
│               → ADD: WalletRefundConsumer class
│
├── shared.contracts/
│   └── Shared.Contracts/
│       └── Events/
│           └── Saga/
│               └── SagaOrchestrationEvents.cs
│                   → ADD: WalletDeducted to IStartPaymentOrchestration
│
└── src/server/gateway/
    └── ocelot.json
        → ADD: Wallet API routes
```

---

## Payment Flow (After Implementation)

### Success Flow
```
1. User tops up wallet: ₹10,000
2. User pays bill: ₹10,000
3. System checks: Wallet >= Bill ✓
4. Deduct from wallet: Wallet = ₹0
5. Saga runs: Update Bill ✓ → Deduct Card ✓
6. Complete: Payment Successful!
7. User sees: Wallet ₹0, Bill Paid
```

### Failure Flow (Rollback)
```
1. User tops up wallet: ₹10,000
2. User pays bill: ₹10,000
3. Deduct from wallet: Wallet = ₹0
4. Saga runs: Update Bill ✓ → FAIL at Card Deduction
5. Compensating: Revert Bill ✓
6. REFUND WALLET: Wallet = ₹10,000 (REFUNDED!)
7. User sees: Wallet ₹10,000 (REFUNDED!), Payment Failed
```

---

## Rollback Order

| Step | Action | Rollback |
|------|--------|----------|
| 1 | Wallet Deducted | ❌ Refund to Wallet |
| 2 | Bill Updated | ❌ Revert Bill |
| 3 | Card Deducted | ❌ (handled by Bill Revert) |
| 4 | Reward Redeemed | ❌ (if applicable) |

---

## Questions to Answer Before Implementation

1. [ ] Should wallet be auto-created on first topup OR on user registration?
2. [ ] Minimum topup amount? (e.g., ₹100)
3. [ ] Maximum wallet balance? (unlimited or cap?)
4. [ ] Auto-create wallet when user registers?

---

## Next Steps

1. Review and approve this plan
2. Answer questions above
3. Begin implementation

---
