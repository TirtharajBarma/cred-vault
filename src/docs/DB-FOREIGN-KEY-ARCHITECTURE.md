# Database Architecture (Tables, Columns, and Relationships)

This document shows the current SQL database architecture from EF Core `DbContext` + model snapshots:

1. Every database
2. Every table in each database
3. Key columns in each table
4. Relationship type and cardinality (`1:N`, `N:1`, `1:1`)

## Database Overview

| Service | Database | Tables | SQL FK Count |
|---|---|---:|---:|
| Billing | `credvault_billing` | 6 | 3 |
| Card | `credvault_cards` | 3 | 2 |
| Payment | `credvault_payments` | 3 | 1 |
| Identity | `credvault_identity` | 1 | 0 |
| Notification | `credvault_notifications` | 2 | 0 |

## Global Architecture Diagram

```mermaid
flowchart LR
    subgraph B[credvault_billing]
        B1[Bills]
        B2[RewardAccounts]
        B3[RewardTiers]
        B4[RewardTransactions]
        B5[Statements]
        B6[StatementTransactions]

        B2 -->|1:N| B4
        B1 -->|1:N optional| B4
        B5 -->|1:N| B6
    end

    subgraph C[credvault_cards]
        C1[CardIssuers]
        C2[CreditCards]
        C3[CardTransactions]

        C1 -->|1:N| C2
        C2 -->|1:N| C3
    end

    subgraph P[credvault_payments]
        P1[Payments]
        P2[Transactions]
        P3[PaymentOrchestrationSagas]

        P1 -->|1:N| P2
    end

    subgraph I[credvault_identity]
        I1[identity_users]
    end

    subgraph N[credvault_notifications]
        N1[AuditLogs]
        N2[NotificationLogs]
    end
```

## Detailed ER Diagrams By Database

### Billing Database (`credvault_billing`)

```mermaid
erDiagram
    Bills {
        uniqueidentifier Id PK
        uniqueidentifier UserId
        uniqueidentifier CardId
        int CardNetwork
        uniqueidentifier IssuerId
        decimal Amount
        decimal MinDue
        string Currency
        datetime BillingDateUtc
        datetime DueDateUtc
        int Status
        datetime CreatedAtUtc
        datetime UpdatedAtUtc
        datetime PaidAtUtc
        decimal AmountPaid
    }

    RewardTiers {
        uniqueidentifier Id PK
        int CardNetwork
        uniqueidentifier IssuerId
        decimal MinSpend
        decimal RewardRate
        datetime EffectiveFromUtc
        datetime EffectiveToUtc
        datetime CreatedAtUtc
        datetime UpdatedAtUtc
    }

    RewardAccounts {
        uniqueidentifier Id PK
        uniqueidentifier UserId
        uniqueidentifier RewardTierId
        decimal PointsBalance
        datetime CreatedAtUtc
        datetime UpdatedAtUtc
    }

    RewardTransactions {
        uniqueidentifier Id PK
        uniqueidentifier RewardAccountId FK
        uniqueidentifier BillId FK
        decimal Points
        int Type
        datetime CreatedAtUtc
        datetime ReversedAtUtc
    }

    Statements {
        uniqueidentifier Id PK
        uniqueidentifier UserId
        uniqueidentifier CardId
        uniqueidentifier BillId
        string StatementPeriod
        string CardLast4
        string CardNetwork
        string IssuerName
        decimal OpeningBalance
        decimal TotalPurchases
        decimal TotalPayments
        decimal TotalRefunds
        decimal PenaltyCharges
        decimal InterestCharges
        decimal ClosingBalance
        decimal MinimumDue
        decimal AmountPaid
        decimal CreditLimit
        decimal AvailableCredit
        datetime PeriodStartUtc
        datetime PeriodEndUtc
        datetime GeneratedAtUtc
        datetime DueDateUtc
        datetime PaidAtUtc
        int Status
        string Notes
        datetime CreatedAtUtc
        datetime UpdatedAtUtc
    }

    StatementTransactions {
        uniqueidentifier Id PK
        uniqueidentifier StatementId FK
        string Type
        decimal Amount
        string Description
        datetime DateUtc
        datetime CreatedAtUtc
        uniqueidentifier SourceTransactionId
    }

    RewardAccounts ||--o{ RewardTransactions : "1:N via RewardAccountId"
    Bills ||--o{ RewardTransactions : "1:N via BillId (nullable FK)"
    Statements ||--o{ StatementTransactions : "1:N via StatementId"
```

### Card Database (`credvault_cards`)

```mermaid
erDiagram
    CardIssuers {
        uniqueidentifier Id PK
        string Name
        int Network
        datetime CreatedAtUtc
        datetime UpdatedAtUtc
    }

    CreditCards {
        uniqueidentifier Id PK
        uniqueidentifier UserId
        uniqueidentifier IssuerId FK
        string CardholderName
        string Last4
        string MaskedNumber
        string EncryptedCardNumber
        int ExpMonth
        int ExpYear
        decimal CreditLimit
        decimal OutstandingBalance
        int BillingCycleStartDay
        bool IsDefault
        bool IsVerified
        datetime VerifiedAtUtc
        bool IsDeleted
        datetime DeletedAtUtc
        datetime CreatedAtUtc
        datetime UpdatedAtUtc
    }

    CardTransactions {
        uniqueidentifier Id PK
        uniqueidentifier CardId FK
        uniqueidentifier UserId
        int Type
        decimal Amount
        string Description
        datetime DateUtc
        datetime CreatedAtUtc
    }

    CardIssuers ||--o{ CreditCards : "1:N via IssuerId"
    CreditCards ||--o{ CardTransactions : "1:N via CardId"
```

### Payment Database (`credvault_payments`)

```mermaid
erDiagram
    Payments {
        uniqueidentifier Id PK
        uniqueidentifier UserId
        uniqueidentifier CardId
        uniqueidentifier BillId
        decimal Amount
        int PaymentType
        int Status
        string FailureReason
        string OtpCode
        datetime OtpExpiresAtUtc
        datetime CreatedAtUtc
        datetime UpdatedAtUtc
    }

    Transactions {
        uniqueidentifier Id PK
        uniqueidentifier PaymentId FK
        uniqueidentifier UserId
        decimal Amount
        int Type
        string Description
        datetime CreatedAtUtc
    }

    PaymentOrchestrationSagas {
        uniqueidentifier CorrelationId PK
        uniqueidentifier UserId
        uniqueidentifier PaymentId
        uniqueidentifier CardId
        uniqueidentifier BillId
        decimal Amount
        string CurrentState
        string FullName
        string Email
        string PaymentType
        string OtpCode
        datetime OtpExpiresAtUtc
        bool OtpVerified
        bool PaymentProcessed
        bool CardDeducted
        bool BillUpdated
        bool RewardsRedeemed
        decimal RewardsAmount
        int CompensationAttempts
        string CompensationReason
        string PaymentError
        string CardDeductionError
        string BillUpdateError
        datetime CreatedAtUtc
        datetime UpdatedAtUtc
    }

    Payments ||--o{ Transactions : "1:N via PaymentId"
```

### Identity Database (`credvault_identity`)

```mermaid
erDiagram
    identity_users {
        uniqueidentifier Id PK
        string FullName
        string Email
        string PasswordHash
        string Role
        string Status
        bool IsEmailVerified
        string EmailVerificationOtp
        datetime EmailVerificationOtpExpiresAtUtc
        string PasswordResetOtp
        datetime PasswordResetOtpExpiresAtUtc
        datetime CreatedAtUtc
        datetime UpdatedAtUtc
    }
```

### Notification Database (`credvault_notifications`)

```mermaid
erDiagram
    AuditLogs {
        uniqueidentifier Id PK
        string EntityName
        string EntityId
        string Action
        string Changes
        string UserId
        string TraceId
        datetime CreatedAtUtc
    }

    NotificationLogs {
        uniqueidentifier Id PK
        string Type
        string Recipient
        string Subject
        string Body
        bool IsSuccess
        string ErrorMessage
        uniqueidentifier UserId
        string TraceId
        datetime CreatedAtUtc
    }
```

## Relationship Cardinality Matrix

| Database | Parent Table | Child Table | FK Column | Parent to Child | Child to Parent | On Delete |
|---|---|---|---|---|---|---|
| `credvault_billing` | `RewardAccounts` | `RewardTransactions` | `RewardAccountId` | `1:N` | `N:1` | `CASCADE` |
| `credvault_billing` | `Bills` | `RewardTransactions` | `BillId` (nullable) | `1:N` (optional link) | `N:1` (optional) | `RESTRICT` |
| `credvault_billing` | `Statements` | `StatementTransactions` | `StatementId` | `1:N` | `N:1` | `CASCADE` |
| `credvault_cards` | `CardIssuers` | `CreditCards` | `IssuerId` | `1:N` | `N:1` | `RESTRICT` |
| `credvault_cards` | `CreditCards` | `CardTransactions` | `CardId` | `1:N` | `N:1` | `RESTRICT` |
| `credvault_payments` | `Payments` | `Transactions` | `PaymentId` | `1:N` | `N:1` | `CASCADE` |

Current schema has no `1:1` SQL foreign key relationship.

## Important Architecture Note

You will see many cross-service ID columns like `UserId`, `BillId`, and `CardId` in multiple databases. These are application-level references for microservice boundaries, not SQL-enforced foreign keys across databases.

## Logical Links Without SQL FK Constraint

These columns look relational but are not enforced as SQL foreign keys in the current model:

| Database | Table.Column | Logical Target | Notes |
|---|---|---|---|
| `credvault_billing` | `RewardAccounts.RewardTierId` | `RewardTiers.Id` | Same DB reference pattern, no FK configured |
| `credvault_billing` | `Statements.BillId` | `Bills.Id` | Same DB reference pattern, no FK configured |
| `credvault_payments` | `PaymentOrchestrationSagas.PaymentId` | `Payments.Id` | Workflow/saga linkage, no FK configured |
| `credvault_payments` | `Payments.BillId` | Billing `Bills.Id` | Cross-service ID reference |
| `credvault_payments` | `Payments.CardId` | Card `CreditCards.Id` | Cross-service ID reference |
| `credvault_cards` | `CreditCards.UserId` | Identity `identity_users.Id` | Cross-service ID reference |
| `credvault_billing` | `Bills.UserId` / `Statements.UserId` | Identity `identity_users.Id` | Cross-service ID references |
| `credvault_notifications` | `NotificationLogs.UserId` | Identity `identity_users.Id` | Cross-service ID reference |

## Source of Truth Used

- `server/services/billing-service/BillingService.Infrastructure/Persistence/Sql/BillingDbContext.cs`
- `server/services/billing-service/BillingService.Infrastructure/Persistence/Sql/Migrations/BillingDbContextModelSnapshot.cs`
- `server/services/card-service/CardService.Infrastructure/Persistence/Sql/CardDbContext.cs`
- `server/services/card-service/CardService.Infrastructure/Persistence/Sql/Migrations/CardDbContextModelSnapshot.cs`
- `server/services/payment-service/PaymentService.Infrastructure/Persistence/Sql/PaymentDbContext.cs`
- `server/services/payment-service/PaymentService.Infrastructure/Migrations/PaymentDbContextModelSnapshot.cs`
- `server/services/identity-service/IdentityService.Infrastructure/Persistence/Sql/Migrations/IdentityDbContextModelSnapshot.cs`
- `server/services/notification-service/NotificationService.Infrastructure/Migrations/NotificationDbContextModelSnapshot.cs`
