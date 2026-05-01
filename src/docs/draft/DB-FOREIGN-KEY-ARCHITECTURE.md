# Database Architecture — CredVault
**System:** CredVault Credit Card Management Platform  
**Version:** 1.0  
**Date:** April 2026
---
## 1. Overview
CredVault follows the **database-per-service** pattern. Each of the 5 microservices owns its own isolated SQL Server database. There are **no cross-service foreign key constraints** — referential integrity across services is maintained through event-driven communication and saga orchestration.
| Service | Database | Tables | ORM |
|---------|----------|:------:|-----|
| Identity | `credvault_identity` | 1 | EF Core (Code-First) |
| Card | `credvault_cards` | 3 | EF Core (Code-First) |
| Billing | `credvault_billing` | 6 | EF Core (Code-First) |
| Payment | `credvault_payments` | 6 | EF Core (Code-First) |
| Notification | `credvault_notifications` | 2 | EF Core (Code-First) |
**Total:** 5 databases, 18 tables, 0 cross-service FK constraints.
---
## 2. Cross-Service Relationship Architecture
### 2.1 The Core Principle
```mermaid
flowchart LR
    CI[("CardIssuers")] -->|1:N| CC[("CreditCards")] -->|1:N| CT[("CardTransactions")]
    RTi[("RewardTiers")] -->|1:N| RAc[("RewardAccounts")] -->|1:N| RTr[("RewardTransactions")]
    BI[("Bills")] -->|1:N| ST[("Statements")] -->|1:N| STr[("StatementTransactions")]
    BI -->|1:N| RTr
    PA[("Payments")] -->|1:N| TR[("Transactions")]
    UW[("UserWallets")] -->|1:N| WT[("WalletTransactions")]
```
**Legend:** Solid arrows = Physical FKs (within same DB). No arrows between services = No cross-service FKs.
### 2.2 Logical References (GUIDs, No FK Constraints)
```mermaid
flowchart LR
    U[("identity_users.Id (PK)")]
    CC[CreditCards.UserId]
    CT[CardTransactions.UserId]
    BI[Bills.UserId]
    ST[Statements.UserId]
    RA[RewardAccounts.UserId]
    PA[Payments.UserId]
    UW[UserWallets.UserId]
    AL[AuditLogs.UserId]
    NL[NotificationLogs.UserId]
    U -.-> CC & CT & BI & ST & RA & PA & UW & AL & NL
```
The `identity_users.Id` is referenced by 9 columns across 4 databases — none enforced as foreign keys.
### 2.3 Complete Cross-Service Reference Map
| Source Table | Column | Target Table | Consistency |
|--------------|--------|--------------|-------------|
| CreditCards | UserId | identity_users | Event-driven |
| CardTransactions | UserId | identity_users | Event-driven |
| CardTransactions | CardId | CreditCards | **Physical FK** |
| Bills | UserId | identity_users | Event-driven |
| Bills | CardId | CreditCards | Event-driven |
| Bills | IssuerId | CardIssuers | Event-driven |
| Statements | UserId | identity_users | Event-driven |
| Statements | CardId | CreditCards | Event-driven |
| Statements | BillId | Bills | **Physical FK** |
| RewardAccounts | UserId | identity_users | Event-driven |
| RewardAccounts | RewardTierId | RewardTiers | **Physical FK** |
| RewardTransactions | RewardAccountId | RewardAccounts | **Physical FK** |
| RewardTransactions | BillId | Bills | **Physical FK** |
| Payments | UserId | identity_users | Event-driven + Saga |
| Payments | CardId | CreditCards | Event-driven + Saga |
| Payments | BillId | Bills | Event-driven + Saga |
| Transactions | PaymentId | Payments | **Physical FK** |
| UserWallets | UserId | identity_users | Event-driven |
| WalletTransactions | WalletId | UserWallets | **Physical FK** |
| AuditLogs | UserId | identity_users | Logging only |
| NotificationLogs | UserId | identity_users | Logging only |
---
## 3. Per-Service Database Details
### 3.1 credvault_identity (1 table)

```mermaid
flowchart LR
    subgraph identity_users
        C1["Id PK, Email UQ, FullName"]
        C2["PasswordHash nullable, IsEmailVerified"]
        C3["EmailOTP, PasswordResetOTP"]
        C4["Status, Role, CreatedAt, UpdatedAt"]
    end
    C1 --- C2 --- C3 --- C4
```
- **PK:** Id | **UQ:** Email
- `PasswordHash` nullable for Google SSO users
- OTPs are 6-digit, regenerated on each request
### 3.2 credvault_cards (3 tables)

```mermaid
flowchart LR
    subgraph CardIssuers
        I1["Id PK, Name, Network, Timestamps"]
    end
    subgraph CreditCards
        C1["Id PK, UserId idx, IssuerId FK"]
        C2["CardholderName, Last4, MaskedNumber"]
        C3["EncryptedNumber, ExpMonth, ExpYear"]
        C4["CreditLimit, OutstandingBalance"]
        C5["BillingCycleDay, IsDefault, IsVerified"]
        C6["IsDeleted soft, Timestamps"]
    end
    subgraph CardTransactions
        T1["Id PK, CardId FK, UserId idx"]
        T2["Type, Amount, Description, DateUtc"]
    end
    I1 -->|1:N| C1 -->|1:N| T1
    C1 --- C2 --- C3 --- C4 --- C5 --- C6
    T1 --- T2
```
**Physical FKs:** `CreditCards.IssuerId` → `CardIssuers.Id` (Restrict), `CardTransactions.CardId` → `CreditCards.Id` (Restrict)  
**Query Filters:** `IsDeleted = false` on CreditCards (global filter)
### 3.3 credvault_billing (6 tables)

```mermaid
flowchart LR
    subgraph Bills
        B1["Id PK, UserId idx, CardId, IssuerId"]
        B2["Amount, MinDue, Currency INR"]
        B3["BillingDateUtc, DueDateUtc idx"]
        B4["AmountPaid, PaidAtUtc, Status"]
    end
    subgraph Statements
        S1["Id PK, UserId idx, CardId idx, BillId FK"]
        S2["Period, PeriodStart, PeriodEndUtc idx"]
        S3["OpeningBalance, Purchases, Payments, Refunds"]
        S4["Penalties, Interest, ClosingBalance, MinDue"]
        S5["CardLast4, CardNetwork, IssuerName, CreditLimit"]
        S6["Status, Timestamps"]
    end
    subgraph StatementTransactions
        X1["Id PK, StatementId FK Cascade"]
        X2["SourceTransactionId, Type, Amount, DateUtc"]
    end
    subgraph RewardTiers
        T1["Id PK, CardNetwork, IssuerId nullable"]
        T2["MinSpend, RewardRate, EffectiveFrom/To"]
    end
    subgraph RewardAccounts
        A1["Id PK, UserId UQ, RewardTierId FK"]
        A2["PointsBalance, Timestamps"]
    end
    subgraph RewardTransactions
        R1["Id PK, RewardAccountId FK Cascade"]
        R2["BillId FK Restrict, Points, Type, CreatedAt"]
    end
    B1 --- B2 --- B3 --- B4
    S1 --- S2 --- S3 --- S4 --- S5 --- S6
    X1 --- X2
    T1 --- T2
    A1 --- A2
    R1 --- R2
    B1 -->|1:N| S1 -->|1:N| X1
    T1 -->|1:N| A1 -->|1:N| R1
    B1 -->|1:N| R2
```
**Physical FKs:** `Statements.BillId` → `Bills.Id` (Restrict), `StatementTransactions.StatementId` → `Statements.Id` (Cascade), `RewardAccounts.RewardTierId` → `RewardTiers.Id` (Restrict), `RewardTransactions.RewardAccountId` → `RewardAccounts.Id` (Cascade), `RewardTransactions.BillId` → `Bills.Id` (Restrict)
### 3.4 credvault_payments (6 tables)

```mermaid
flowchart LR
    subgraph Payments
        P1["Id PK, UserId idx, CardId, BillId idx"]
        P2["Amount, PaymentType Wallet/Card"]
        P3["Status, FailureReason nullable"]
        P4["OtpCode 6-digit, OtpExpiresAtUtc"]
    end
    subgraph Transactions
        T1["Id PK, PaymentId FK Cascade, UserId idx"]
        T2["Amount, Type Debit/Credit, Description"]
    end
    subgraph UserWallets
        W1["Id PK, UserId UQ"]
        W2["Balance, TotalTopUps, TotalSpent, Timestamps"]
    end
    subgraph WalletTransactions
        X1["Id PK, WalletId FK Cascade"]
        X2["Type TopUp/Debit/Refund, Amount, BalanceAfter"]
        X3["RelatedPaymentId nullable, Description, CreatedAt"]
    end
    subgraph PaymentOrchestrationSagas
        S1["CorrelationId PK, CurrentState"]
        S2["UserId, CardId, BillId, Email, FullName"]
        S3["Amount, RewardsAmount, PaymentType"]
        S4["CompensationReason, ErrorFields"]
    end
    subgraph RazorpayWalletTopUps
        R1["Id PK, UserId idx, Amount"]
        R2["RazorpayOrderId UQ"]
        R3["RazorpayPaymentId UQ nullable, Signature"]
        R4["Status, FailureReason, Timestamps"]
    end
    P1 --- P2 --- P3 --- P4
    T1 --- T2
    W1 --- W2
    X1 --- X2 --- X3
    S1 --- S2 --- S3 --- S4
    R1 --- R2 --- R3 --- R4
    P1 -->|1:N| T1
    W1 -->|1:N| X1
```
**Physical FKs:** `Transactions.PaymentId` → `Payments.Id` (Cascade), `WalletTransactions.WalletId` → `UserWallets.Id` (Cascade)  
**Notes:** `PaymentOrchestrationSagas` is the MassTransit saga state table. `RazorpayOrderId` and `RazorpayPaymentId` have unique constraints for idempotency.
### 3.5 credvault_notifications (2 tables)

```mermaid
flowchart LR
    subgraph AuditLogs
        A1["Id PK, EntityName, EntityId, Action"]
        A2["UserId cross-ref, Changes JSON, TraceId, CreatedAt"]
    end
    subgraph NotificationLogs
        N1["Id PK, UserId cross-ref, Recipient, Subject"]
        N2["Body, Type Email/SMS, IsSuccess, ErrorMessage, TraceId, CreatedAt"]
    end
    A1 --- A2
    N1 --- N2
```
- No physical FKs in this database
- `TraceId` enables distributed request tracing
- Both tables are append-only (no updates/deletes)
---
## 4. Data Consistency Strategy
Since there are no cross-service foreign key constraints, data consistency is maintained through:
| Mechanism | Purpose | Implementation |
|-----------|---------|----------------|
| Saga Pattern | Distributed transaction consistency | MassTransit state machine with compensation |
| Event Publishing | Propagate changes across services | RabbitMQ pub/sub via MassTransit |
| Outbox Pattern | Prevent lost messages | `UseInMemoryOutbox()` on all publishers |
| Retry Policies | Handle transient failures | Exponential backoff: 1s → 5s → 15s |
| Idempotency | Safe message reprocessing | CorrelationId-based deduplication |
| Soft Deletes | Preserve referential integrity | `IsDeleted` flag on CreditCards |
| Denormalization | Reduce cross-service queries | CardLast4, CardNetwork, IssuerName stored in Bills/Statements |
### 4.1 What Happens When a User is Deleted?
```mermaid
flowchart LR
    D1["Delete user"] --> P1["Publish IUserDeleted"]
    P1 -->|"RabbitMQ"| C1["Card: soft-delete cards"]
    P1 -->|"RabbitMQ"| C2["Billing: archive bills"]
    P1 -->|"RabbitMQ"| C3["Payment: archive payments"]
    P1 -->|"RabbitMQ"| C4["Notification: audit entry"]
```
The `IUserDeleted` event consumer is implemented in each service, but the publisher in Identity Service needs to be wired up to a delete endpoint.
### 4.2 What Happens When a Card is Soft-Deleted?
```mermaid
flowchart LR
    SD["Set IsDeleted = true"] --> QF["Excluded from queries (global filter)"]
    SD -.-> BF["Bill queries OK (denormalized)"]
    SD -.-> PF["Payment queries OK (stored GUID)"]
```
Because Bills and Statements store CardLast4, CardNetwork, and IssuerName as denormalized columns, they remain queryable even after a card is deleted.
---
## 5. Physical Foreign Key Summary
All physical FKs exist **only within a single service's database**:
| Service | Table | Column | References | Delete Behavior |
|---------|-------|--------|------------|-----------------|
| Card | CreditCards | IssuerId | CardIssuers.Id | Restrict |
| Card | CardTransactions | CardId | CreditCards.Id | Restrict |
| Billing | Statements | BillId | Bills.Id | Restrict |
| Billing | StatementTransactions | StatementId | Statements.Id | Cascade |
| Billing | RewardAccounts | RewardTierId | RewardTiers.Id | Restrict |
| Billing | RewardTransactions | RewardAccountId | RewardAccounts.Id | Cascade |
| Billing | RewardTransactions | BillId | Bills.Id | Restrict |
| Payment | Transactions | PaymentId | Payments.Id | Cascade |
| Payment | WalletTransactions | WalletId | UserWallets.Id | Cascade |
**Total physical FKs:** 9 | **Total cross-service FKs:** 0
---
## 6. Index Strategy
### Cross-Service Reference Indexes
| Database | Table | Column | Index Type | Purpose |
|----------|-------|--------|------------|---------|
| credvault_cards | CreditCards | UserId | Non-clustered | Find cards by user |
| credvault_cards | CardTransactions | UserId | Non-clustered | Find transactions by user |
| credvault_billing | Bills | UserId | Non-clustered | Find bills by user |
| credvault_billing | Bills | DueDateUtc | Non-clustered | Find overdue bills |
| credvault_billing | Statements | UserId | Non-clustered | Find statements by user |
| credvault_billing | Statements | CardId | Non-clustered | Find statements by card |
| credvault_billing | Statements | PeriodEndUtc | Non-clustered | Find statements by period |
| credvault_billing | RewardAccounts | UserId | **Unique** | One reward account per user |
| credvault_payments | Payments | UserId | Non-clustered | Find payments by user |
| credvault_payments | Payments | BillId | Non-clustered | Find payments by bill |
| credvault_payments | UserWallets | UserId | **Unique** | One wallet per user |
| credvault_payments | WalletTransactions | CreatedAtUtc | Non-clustered | Find transactions by date |
| credvault_payments | RazorpayWalletTopUps | RazorpayOrderId | **Unique** | Idempotent webhook handling |
| credvault_payments | RazorpayWalletTopUps | RazorpayPaymentId | **Unique** | Idempotent webhook handling |
---
*End of Database Architecture Document*