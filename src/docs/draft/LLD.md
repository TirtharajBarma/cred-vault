# Low-Level Design (LLD) — CredVault
**System:** CredVault Credit Card Management Platform  
**Version:** 1.0  
**Date:** April 2026
---
## Table of Contents
1. [Design Principles](#1-design-principles)
2. [Service Architecture](#2-service-architecture)
3. [Sequence Diagrams](#3-sequence-diagrams)
4. [Saga Orchestration](#4-saga-orchestration)
5. [Database Schema](#5-database-schema)
6. [Event Contracts](#6-event-contracts)
7. [State Machines](#7-state-machines)
8. [Business Rules](#8-business-rules)
---
## 1. Design Principles
### 1.1 Clean Architecture
Every microservice follows a strict four-layer structure:
```
┌──────────────────────────────────────────┐
│              API Layer                    │  Controllers, Middleware, DI setup
├──────────────────────────────────────────┤
│          Application Layer                │  Commands, Queries, Handlers, Validators
├──────────────────────────────────────────┤
│        Infrastructure Layer               │  EF Core DbContext, Repositories, Messaging
├──────────────────────────────────────────┤
│            Domain Layer                   │  Entities, Enums, Domain Events
└──────────────────────────────────────────┘
```
**Dependency Rule:** Inner layers never depend on outer layers. The Domain layer has zero external dependencies.
### 1.2 CQRS with MediatR
- **Commands** mutate state (create, update, delete)
- **Queries** read data (fetch, list, search)
- Each handler is isolated — no shared logic between reads and writes
- FluentValidation runs as a pipeline behavior before handlers execute
### 1.3 Saga Pattern for Distributed Transactions
Cross-service operations (bill payment involves Payment → Billing → Card services) use a **choreography-based saga** via MassTransit State Machine:
- Each step publishes an event; the next step reacts to it
- On failure, compensation events reverse completed steps
- State is persisted so sagas survive service restarts
### 1.4 Shared Contracts Library
`Shared.Contracts` is a .NET class library referenced by all services providing:
- `BaseApiController` — standardized response methods
- `ApiResponse<T>` — consistent API envelope
- `ExceptionHandlingMiddleware` — global error handling
- `ServiceCollectionExtensions` — JWT, Swagger, MassTransit setup
- All event interfaces and enum definitions
---
## 2. Service Architecture
### 2.1 Identity Service (:5001)
**Domain Entities:** `IdentityUser`
**Commands:**
| Command | Handler Action |
|---------|---------------|
| `RegisterUserCommand` | Create user (PendingVerification), generate OTP, publish `IUserRegistered` + `IUserOtpGenerated` |
| `LoginUserCommand` | Validate credentials, check status, return JWT |
| `GoogleLoginCommand` | Validate Google IdToken, create/login user, return JWT |
| `VerifyEmailOtpCommand` | Validate OTP, mark user Active, return JWT |
| `ResendVerificationCommand` | Regenerate OTP, publish `IUserOtpGenerated` |
| `ForgotPasswordCommand` | Generate reset OTP, publish `IUserOtpGenerated` |
| `ResetPasswordCommand` | Validate OTP, update password hash |
| `UpdateUserProfileCommand` | Update FullName |
| `ChangePasswordCommand` | Validate old password, set new hash |
| `AdminUpdateUserStatusCommand` | Change user status (Active/Suspended) |
| `AdminUpdateUserRoleCommand` | Change user role (User/Admin) |
**Queries:**
| Query | Returns |
|-------|---------|
| `GetUserProfileQuery` | Current user profile |
| `AdminListUsersQuery` | Paginated user list |
| `AdminGetUserQuery` | Single user details |
| `AdminGetUserStatsQuery` | Aggregated user statistics |
**Events Published:** `IUserRegistered`, `IUserOtpGenerated`
---
### 2.2 Card Service (:5002)
**Domain Entities:** `CreditCard`, `CardTransaction`, `CardIssuer`
**Commands:**
| Command | Handler Action |
|---------|---------------|
| `AddCardCommand` | Encrypt card number, generate masked number, create card, publish `ICardAdded` |
| `UpdateCardCommand` | Update cardholder name, credit limit, billing cycle |
| `DeleteCardCommand` | Set `IsDeleted = true` (soft delete) |
| `SetDefaultCardCommand` | Mark card as default, unset previous default |
| `AddCardTransactionCommand` | Record purchase/payment/refund |
| `AdminUpdateCardCommand` | Admin card update |
| `AddIssuerCommand` | Create card issuer |
| `UpdateIssuerCommand` | Update issuer details |
| `DeleteIssuerCommand` | Delete issuer |
**Queries:**
| Query | Returns |
|-------|---------|
| `GetCardsByUserQuery` | User's cards (excludes soft-deleted) |
| `GetCardByIdQuery` | Single card details |
| `GetCardTransactionsQuery` | Paginated card transactions |
| `GetAllUserTransactionsQuery` | All transactions across user's cards |
| `ListIssuersQuery` | All card issuers |
| `AdminListCardsQuery` | All cards (including soft-deleted) |
**Events Published:** `ICardAdded`
---
### 2.3 Billing Service (:5003)
**Domain Entities:** `Bill`, `Statement`, `StatementTransaction`, `RewardAccount`, `RewardTier`, `RewardTransaction`
**Commands:**
| Command | Handler Action |
|---------|---------------|
| `GenerateBillCommand` | Aggregate transactions, calculate totals/min-due, create bill + statement |
| `CheckOverdueBillsCommand` | Scan bills past due date, mark as Overdue |
| `PayBillCommand` | Update bill status, record payment amount |
| `CreateStatementCommand` | Generate statement from card transactions |
| `EarnRewardsCommand` | Calculate points from tier rate, add to reward account |
| `RedeemRewardsCommand` | Deduct points, create RewardTransaction (Redeemed) |
| `ReverseRewardsCommand` | Restore points, create RewardTransaction (Reversed) — saga compensation |
| `CreateRewardTierCommand` | Create reward tier |
| `UpdateRewardTierCommand` | Update tier configuration |
| `DeleteRewardTierCommand` | Delete reward tier |
**Queries:**
| Query | Returns |
|-------|---------|
| `GetBillsByUserQuery` | User's bills (paginated) |
| `GetBillByIdQuery` | Single bill details |
| `HasPendingBillQuery` | Boolean + bill details if exists |
| `GetStatementsByUserQuery` | User's statements |
| `GetStatementByIdQuery` | Statement with full breakdown |
| `GetRewardAccountQuery` | Reward balance + tier info |
| `GetRewardTransactionsQuery` | Reward history |
| `ListRewardTiersQuery` | All reward tiers |
---
### 2.4 Payment Service (:5004)
**Domain Entities:** `Payment`, `Transaction`, `UserWallet`, `WalletTransaction`, `PaymentOrchestrationSagaState`, `RazorpayWalletTopUp`
**Commands:**
| Command | Handler Action |
|---------|---------------|
| `InitiatePaymentCommand` | Validate bill, check wallet balance, create Payment, generate OTP, publish `IPaymentOtpGenerated` |
| `VerifyPaymentOtpCommand` | Validate OTP, publish `IStartPaymentOrchestration` (triggers saga) |
| `ResendPaymentOtpCommand` | Regenerate OTP |
| `TopUpWalletCommand` | Create Razorpay order |
| `DebitWalletCommand` | Deduct wallet balance — saga step |
| `RefundWalletCommand` | Restore wallet balance — saga compensation |
| `ProcessRazorpayWebhookCommand` | Verify signature, update wallet, mark top-up complete |
**Queries:**
| Query | Returns |
|-------|---------|
| `GetPaymentByIdQuery` | Payment details |
| `GetPaymentTransactionsQuery` | Payment transaction history |
| `GetWalletQuery` | Full wallet info |
| `GetWalletBalanceQuery` | Balance only |
| `GetWalletTransactionsQuery` | Wallet transaction history |
**Events Published:** `IPaymentOtpGenerated`, `IStartPaymentOrchestration`, saga state events
**Background Jobs:** Payment expiration cleanup
---
### 2.5 Notification Service (:5005)
**Domain Entities:** `AuditLog`, `NotificationLog`
**Consumers (MassTransit):**
| Event Consumer | Action |
|----------------|--------|
| `IUserRegistered` | Send welcome email |
| `IUserOtpGenerated` | Send OTP email (verification or password reset) |
| `ICardAdded` | Send card confirmation email |
| `IPaymentOtpGenerated` | Send payment OTP email |
| `IPaymentCompleted` | Send payment success email |
| `IPaymentCompensated` | Send payment failure email |
**Commands:**
| Command | Handler Action |
|---------|---------------|
| `SendEmailCommand` | Send via Gmail SMTP, log result |
| `LogAuditCommand` | Create audit log entry |
| `LogNotificationCommand` | Create notification log entry |
**Queries:**
| Query | Returns |
|-------|---------|
| `GetNotificationLogsQuery` | Paginated notification logs (admin) |
| `GetAuditLogsQuery` | Paginated audit logs (admin) |
---
## 3. Sequence Diagrams
### 3.1 User Registration & Email Verification
```mermaid
sequenceDiagram
    participant U as User
    participant UI as Angular Client
    participant GW as API Gateway
    participant IS as Identity Service
    participant DB as Identity DB
    participant MQ as RabbitMQ
    participant NS as Notification Service
    participant SMTP as Gmail SMTP
    U->>UI: Enter email, name, password
    UI->>GW: POST /api/v1/identity/auth/register
    GW->>IS: Forward request
    IS->>IS: Validate input (email format, password strength)
    IS->>DB: Check email uniqueness
    IS->>DB: Create user (PendingVerification)
    IS->>IS: Generate 6-digit OTP (10-min expiry)
    IS->>MQ: Publish IUserRegistered + IUserOtpGenerated
    IS-->>GW: 201 Created (userId, status)
    GW-->>UI: Registration success response
    UI-->>U: Show OTP input screen
    MQ->>NS: Consume IUserRegistered + IUserOtpGenerated
    NS->>SMTP: Send welcome email with OTP
    SMTP-->>NS: Email sent
    NS->>DB: Log notification (NotificationLogs)
    U->>UI: Enter OTP from email
    UI->>GW: POST /api/v1/identity/auth/verify-email-otp
    GW->>IS: Forward request
    IS->>DB: Validate OTP (match + not expired)
    IS->>DB: Mark user Active
    IS->>IS: Generate JWT token
    IS-->>GW: 200 OK (JWT + user profile)
    GW-->>UI: Auth success response
    UI->>UI: Store JWT in sessionStorage
    UI-->>U: Redirect to dashboard
```
---
### 3.2 Bill Payment with Saga Orchestration
```mermaid
sequenceDiagram
    participant U as User
    participant UI as Angular Client
    participant GW as API Gateway
    participant PS as Payment Service
    participant DB_P as Payment DB
    participant MQ as RabbitMQ
    participant BS as Billing Service
    participant DB_B as Billing DB
    participant CS as Card Service
    participant DB_C as Card DB
    participant NS as Notification Service
    participant SMTP as Gmail SMTP
    Note over U,SMTP: Step 1: Initiate Payment
    U->>UI: Select bill, enter amount, click Pay
    UI->>GW: POST /api/v1/payments/initiate
    GW->>PS: Forward request
    PS->>DB_P: Validate bill status (Pending/PartiallyPaid)
    PS->>DB_P: Check wallet balance (if Wallet payment)
    PS->>DB_P: Create Payment record (Initiated)
    PS->>PS: Generate 6-digit OTP (5-min expiry)
    PS->>MQ: Publish IPaymentOtpGenerated
    PS-->>GW: 201 Created (paymentId, status)
    GW-->>UI: Payment initiated response
    UI-->>U: Show OTP input screen
    MQ->>NS: Consume IPaymentOtpGenerated
    NS->>SMTP: Send payment OTP email
    SMTP-->>NS: Email sent
    Note over U,SMTP: Step 2: Verify OTP & Execute Saga
    U->>UI: Enter OTP from email
    UI->>GW: POST /api/v1/payments/{id}/verify-otp
    GW->>PS: Forward request
    PS->>DB_P: Validate OTP (match + not expired)
    PS->>MQ: Publish IStartPaymentOrchestration
    PS-->>GW: 200 OK (Processing)
    GW-->>UI: "Payment processing" response
    Note over PS,CS: Step 3: Saga Execution (async)
    PS->>PS: Saga: AwaitingBillUpdate
    PS->>MQ: Send IBillUpdateRequested
    MQ->>BS: Consume bill update event
    BS->>DB_B: Update bill status, record payment
    BS->>MQ: Publish IBillUpdated
    MQ->>PS: Receive bill update confirmation
    alt rewardsAmount > 0
        PS->>PS: Saga: AwaitingRewardRedemption
        PS->>MQ: Send IRewardRedemptionRequested
        MQ->>BS: Consume reward redemption event
        BS->>DB_B: Deduct reward points
        BS->>MQ: Publish IRewardsRedeemed
        MQ->>PS: Receive reward confirmation
    end
    PS->>PS: Saga: AwaitingCardDeduction
    PS->>MQ: Send ICardDeductionRequested
    MQ->>CS: Consume card deduction event
    CS->>DB_C: Update card outstanding balance
    CS->>MQ: Publish ICardDeducted
    MQ->>PS: Receive card deduction confirmation
    PS->>PS: Saga: Completed
    PS->>DB_P: Mark payment Paid
    PS->>MQ: Publish IPaymentCompleted
    MQ->>NS: Consume payment completed event
    NS->>SMTP: Send payment success email
```
---
### 3.3 Saga Compensation (Payment Failure)
```mermaid
sequenceDiagram
    participant PS as Payment Service
    participant MQ as RabbitMQ
    participant BS as Billing Service
    participant CS as Card Service
    participant NS as Notification Service
    participant SMTP as Gmail SMTP
    Note over PS,SMTP: Failure occurs at Card Deduction step
    CS->>MQ: Publish ICardDeductionFailed
    MQ->>PS: Receive failure event
    PS->>PS: Saga: Enter Compensating state
    PS->>PS: Determine compensation reason
    alt rewards were redeemed
        PS->>MQ: Send IReverseRewards
        MQ->>BS: Consume reverse rewards event
        BS->>BS: Restore reward points
        BS->>MQ: Publish IRewardsReversed
        MQ->>PS: Receive confirmation
    end
    PS->>MQ: Send IRevertBillUpdate
    MQ->>BS: Consume revert bill event
    BS->>BS: Restore bill to previous status
    BS->>MQ: Publish IBillReverted
    MQ->>PS: Receive confirmation
    PS->>PS: Mark payment Compensated
    PS->>MQ: Publish IPaymentCompensated
    MQ->>NS: Consume payment compensated event
    NS->>SMTP: Send payment failure email
    SMTP-->>NS: Email sent
```
---
### 3.4 Card Addition Flow
```mermaid
sequenceDiagram
    participant U as User
    participant UI as Angular Client
    participant GW as API Gateway
    participant CS as Card Service
    participant DB as Card DB
    participant MQ as RabbitMQ
    participant NS as Notification Service
    participant SMTP as Gmail SMTP
    U->>UI: Enter card details (number, expiry, issuer, limit)
    UI->>GW: POST /api/v1/cards
    GW->>CS: Forward request
    CS->>CS: Validate card number (Luhn check)
    CS->>CS: Verify expiry date (not expired)
    CS->>CS: Encrypt card number (AES)
    CS->>CS: Generate masked number (**** **** **** XXXX)
    CS->>DB: Create CreditCard record
    CS->>MQ: Publish ICardAdded
    CS-->>GW: 201 Created (card details, masked)
    GW-->>UI: Card added response
    UI-->>U: Success message
    MQ->>NS: Consume ICardAdded
    NS->>SMTP: Send card confirmation email
    SMTP-->>NS: Email sent
    NS->>DB: Log notification
```
---
### 3.5 Wallet Top-Up via Razorpay
```mermaid
sequenceDiagram
    participant U as User
    participant UI as Angular Client
    participant GW as API Gateway
    participant PS as Payment Service
    participant DB as Payment DB
    participant RP as Razorpay API
    Note over U,RP: Step 1: Initiate Top-Up
    U->>UI: Enter top-up amount
    UI->>GW: POST /api/v1/wallets/topup
    GW->>PS: Forward request
    PS->>RP: Create Razorpay Order
    RP-->>PS: Return orderId, amount, currency
    PS->>DB: Create RazorpayWalletTopUp record (Pending)
    PS-->>GW: 200 OK (razorpayOrderId)
    GW-->>UI: Order details response
    Note over U,RP: Step 2: User Completes Payment on Razorpay
    UI->>UI: Open Razorpay Checkout (with orderId)
    U->>RP: Complete payment on Razorpay UI
    RP->>RP: Process payment
    Note over U,RP: Step 3: Webhook Callback
    RP->>PS: POST /api/v1/wallets/razorpay-webhook
    PS->>PS: Verify HMAC-SHA256 signature
    PS->>DB: Update RazorpayWalletTopUp (Completed)
    PS->>DB: Update UserWallet balance (+ amount)
    PS->>DB: Create WalletTransaction (TopUp)
    PS-->>RP: 200 OK (webhook acknowledged)
    U->>UI: Check wallet balance
    UI->>GW: GET /api/v1/wallets/balance
    GW->>PS: Forward request
    PS->>DB: Fetch wallet balance
    PS-->>GW: 200 OK (updated balance)
    GW-->>UI: Updated balance
    UI-->>U: Display new balance
```
---
### 3.6 Google OAuth Login Flow
```mermaid
sequenceDiagram
    participant U as User
    participant UI as Angular Client
    participant GW as API Gateway
    participant IS as Identity Service
    participant DB as Identity DB
    participant Google as Google OAuth
    Note over U,Google: Step 1: Google Sign-In
    U->>UI: Click "Sign in with Google"
    UI->>Google: Initiate Google OAuth flow
    Google->>U: Google login/consent screen
    U->>Google: Authenticate & consent
    Google-->>UI: Return Google IdToken
    Note over U,Google: Step 2: Backend Validation
    UI->>GW: POST /api/v1/identity/auth/google (IdToken)
    GW->>IS: Forward request
    IS->>Google: Validate IdToken with public keys
    Google-->>IS: Token valid, claims returned
    IS->>DB: Check if user exists (by email)
    alt User exists
        IS->>DB: Login existing user
        IS->>IS: Generate JWT
    else User does not exist
        IS->>DB: Create user (Active, no password)
        IS->>MQ: Publish IUserRegistered
        IS->>IS: Generate JWT
    end
    IS-->>GW: 200 OK (JWT + user profile)
    GW-->>UI: Auth success response
    UI->>UI: Store JWT in sessionStorage
    UI-->>U: Redirect to dashboard
```
---
## 4. Saga Orchestration
### 4.1 Payment Saga State Machine
The Payment Service uses a MassTransit State Machine to orchestrate distributed bill payments across Billing and Card services.
```mermaid
stateDiagram-v2
    [*] --> Initial
    Initial --> AwaitingOtpVerification: IStartPaymentOrchestration
    AwaitingOtpVerification --> AwaitingPaymentConfirmation: OtpVerified
    AwaitingOtpVerification --> Failed: OtpExpired
    AwaitingPaymentConfirmation --> AwaitingBillUpdate: PaymentConfirmed
    AwaitingBillUpdate --> AwaitingRewardRedemption: BillUpdated AND rewards > 0
    AwaitingBillUpdate --> AwaitingCardDeduction: BillUpdated AND rewards == 0
    AwaitingBillUpdate --> Compensating: BillUpdateFailed
    AwaitingRewardRedemption --> AwaitingCardDeduction: RewardsRedeemed
    AwaitingRewardRedemption --> Compensating: RewardRedemptionFailed
    AwaitingCardDeduction --> Completed: CardDeducted
    AwaitingCardDeduction --> Compensating: CardDeductionFailed
    Compensating --> Compensated: AllCompensationsComplete
    Compensating --> Failed: CompensationFailed
    Completed --> [*]
    Compensated --> [*]
    Failed --> [*]
```
### 4.2 Saga Steps & Compensation
| Step | Action | Target Service | Compensation |
|------|--------|----------------|-------------|
| 1 | Update bill (mark Paid/PartiallyPaid) | Billing Service | Revert bill status |
| 2 | Redeem reward points (if applicable) | Billing Service | Reverse points to reward account |
| 3 | Deduct card outstanding balance | Card Service | Restore card balance |
| 4 | Debit wallet (if wallet payment) | Payment Service | Refund wallet balance |
### 4.3 Saga Reliability
| Mechanism | Configuration |
|-----------|---------------|
| **Outbox** | `UseInMemoryOutbox()` — prevents lost messages during restart |
| **Retry** | Exponential backoff: 1s → 5s → 15s (3 attempts) |
| **Timeout** | 30 seconds per step; triggers compensation |
| **Idempotency** | CorrelationId (GUID) as saga PK; duplicates ignored for completed sagas |
| **Persistence** | `PaymentOrchestrationSagas` table tracks current state |
---
## 5. Database Schema
### 5.1 credvault_identity
| Table | Key Columns | Relationships |
|-------|-------------|---------------|
| `identity_users` | Id (PK), Email (UQ), FullName, PasswordHash, IsEmailVerified, EmailVerificationOtp, PasswordResetOtp, Status, Role, CreatedAtUtc, UpdatedAtUtc | — |
### 5.2 credvault_cards
| Table | Key Columns | Relationships |
|-------|-------------|---------------|
| `CardIssuers` | Id (PK), Name, Network (enum), CreatedAtUtc, UpdatedAtUtc | 1 → N CreditCards |
| `CreditCards` | Id (PK), UserId (indexed), IssuerId (FK), CardholderName, Last4, MaskedNumber, EncryptedCardNumber, ExpMonth, ExpYear, CreditLimit, OutstandingBalance, BillingCycleStartDay, IsDefault, IsVerified, IsDeleted, CreatedAtUtc, UpdatedAtUtc | N → 1 CardIssuers; 1 → N CardTransactions |
| `CardTransactions` | Id (PK), CardId (FK), UserId, Type (enum), Amount, Description, DateUtc | N → 1 CreditCards |
### 5.3 credvault_billing
| Table | Key Columns | Relationships |
|-------|-------------|---------------|
| `Bills` | Id (PK), UserId, CardId, CardNetwork, IssuerId, Amount, MinDue, Currency, BillingDateUtc, DueDateUtc, AmountPaid, PaidAtUtc, Status (enum), CreatedAtUtc, UpdatedAtUtc | 1 → N Statements; 1 → N RewardTransactions |
| `Statements` | Id (PK), UserId, CardId, BillId (FK), StatementPeriod, PeriodStartUtc, PeriodEndUtc, OpeningBalance, TotalPurchases, TotalPayments, TotalRefunds, PenaltyCharges, InterestCharges, ClosingBalance, MinimumDue, AmountPaid, Status, CardLast4, CardNetwork, IssuerName, CreditLimit, AvailableCredit, CreatedAtUtc, UpdatedAtUtc | N → 1 Bills; 1 → N StatementTransactions |
| `StatementTransactions` | Id (PK), StatementId (FK), SourceTransactionId, Type, Amount, Description, DateUtc | N → 1 Statements |
| `RewardAccounts` | Id (PK), UserId (UQ), RewardTierId (FK), PointsBalance, CreatedAtUtc, UpdatedAtUtc | N → 1 RewardTiers; 1 → N RewardTransactions |
| `RewardTiers` | Id (PK), CardNetwork, IssuerId (nullable), MinSpend, RewardRate, EffectiveFromUtc, EffectiveToUtc, CreatedAtUtc, UpdatedAtUtc | 1 → N RewardAccounts |
| `RewardTransactions` | Id (PK), RewardAccountId (FK), BillId (FK, nullable), Points, Type (enum), CreatedAtUtc | N → 1 RewardAccounts; N → 1 Bills |
### 5.4 credvault_payments
| Table | Key Columns | Relationships |
|-------|-------------|---------------|
| `Payments` | Id (PK), UserId, CardId, BillId, Amount, PaymentType (enum), Status (enum), FailureReason, OtpCode, OtpExpiresAtUtc, CreatedAtUtc, UpdatedAtUtc | 1 → N Transactions |
| `Transactions` | Id (PK), PaymentId (FK), UserId, Amount, Type (enum), Description, CreatedAtUtc, UpdatedAtUtc | N → 1 Payments |
| `UserWallets` | Id (PK), UserId (UQ), Balance, TotalTopUps, TotalSpent, CreatedAtUtc, UpdatedAtUtc | 1 → N WalletTransactions |
| `WalletTransactions` | Id (PK), WalletId (FK), Type (enum), Amount, BalanceAfter, Description, RelatedPaymentId, CreatedAtUtc | N → 1 UserWallets |
| `PaymentOrchestrationSagas` | CorrelationId (PK), CurrentState, UserId, CardId, BillId, Email, FullName, Amount, RewardsAmount, PaymentType, CompensationReason, error fields | — |
| `RazorpayWalletTopUps` | Id (PK), UserId, Amount, RazorpayOrderId (UQ), RazorpayPaymentId (UQ), RazorpaySignature, Status, FailureReason, CreatedAtUtc, UpdatedAtUtc | — |
### 5.5 credvault_notifications
| Table | Key Columns | Relationships |
|-------|-------------|---------------|
| `AuditLogs` | Id (PK), EntityName, EntityId, Action, UserId, Changes (JSON), TraceId, CreatedAtUtc | — |
| `NotificationLogs` | Id (PK), UserId, Recipient, Subject, Body, Type (enum), IsSuccess, ErrorMessage, TraceId, CreatedAtUtc | — |
### 5.6 Cross-Database Constraint Policy
**No cross-service foreign key constraints.** Each database is fully isolated. Referential integrity across services is maintained through:
- Eventual consistency via RabbitMQ events
- Saga compensation for distributed transactions
- UserId/CardId/BillId passed as plain GUIDs in events and commands
---
## 6. Event Contracts
### 6.1 Identity Events
```typescript
interface IUserRegistered {
  userId: string;
  email: string;
  fullName: string;
  role: string;
  registeredAt: Date;
}
interface IUserOtpGenerated {
  userId: string;
  email: string;
  otp: string;
  otpType: string;  // "EmailVerification" | "PasswordReset"
  expiresAt: Date;
}
```
### 6.2 Card Events
```typescript
interface ICardAdded {
  cardId: string;
  userId: string;
  cardLast4: string;
  cardNetwork: string;
  issuerName: string;
  addedAt: Date;
}
```
### 6.3 Payment Events
```typescript
interface IPaymentOtpGenerated {
  paymentId: string;
  userId: string;
  email: string;
  otp: string;
  amount: number;
  expiresAt: Date;
}
interface IStartPaymentOrchestration {
  correlationId: string;
  paymentId: string;
  userId: string;
  cardId: string;
  billId: string;
  email: string;
  fullName: string;
  amount: number;
  rewardsAmount: number;
  paymentType: string;  // "Wallet" | "Card"
}
```
### 6.4 Saga Events
```typescript
interface IBillUpdateRequested {
  correlationId: string;
  billId: string;
  amount: number;
  paymentId: string;
}
interface IBillUpdated {
  correlationId: string;
  billId: string;
  success: boolean;
}
interface IRewardRedemptionRequested {
  correlationId: string;
  userId: string;
  rewardsAmount: number;
  billId: string;
}
interface IRewardsRedeemed {
  correlationId: string;
  pointsRedeemed: number;
  success: boolean;
}
interface ICardDeductionRequested {
  correlationId: string;
  cardId: string;
  amount: number;
}
interface ICardDeducted {
  correlationId: string;
  cardId: string;
  success: boolean;
}
interface IPaymentCompleted {
  correlationId: string;
  paymentId: string;
  completedAt: Date;
}
interface IPaymentCompensated {
  correlationId: string;
  paymentId: string;
  compensationReason: string;
  compensatedAt: Date;
}
```
### 6.5 Event Routing
| Event | Publisher | Consumer(s) |
|-------|-----------|-------------|
| `IUserRegistered` | Identity | Notification |
| `IUserOtpGenerated` | Identity | Notification |
| `ICardAdded` | Card | Notification |
| `IPaymentOtpGenerated` | Payment | Notification |
| `IStartPaymentOrchestration` | Payment | Payment (Saga) |
| `IBillUpdateRequested` | Payment (Saga) | Billing |
| `IBillUpdated` | Billing | Payment (Saga) |
| `IRewardRedemptionRequested` | Payment (Saga) | Billing |
| `IRewardsRedeemed` | Billing | Payment (Saga) |
| `ICardDeductionRequested` | Payment (Saga) | Card |
| `ICardDeducted` | Card | Payment (Saga) |
| `IPaymentCompleted` | Payment (Saga) | Notification |
| `IPaymentCompensated` | Payment (Saga) | Notification |
---
## 7. State Machines
### 7.1 User Status
```mermaid
stateDiagram-v2
    [*] --> PendingVerification: Register
    PendingVerification --> Active: Verify Email OTP
    Active --> Suspended: Admin Suspends
    Suspended --> Active: Admin Activates
```
| Status | Description |
|--------|-------------|
| `PendingVerification` | Just registered, awaiting email OTP verification |
| `Active` | Verified and can use all features |
| `Suspended` | Disabled by admin; cannot login |
### 7.2 Bill Status
```mermaid
stateDiagram-v2
    [*] --> Pending: Bill Generated
    Pending --> PartiallyPaid: Partial Payment
    PartiallyPaid --> Paid: Full Payment
    Pending --> Paid: Full Payment
    Pending --> Overdue: Past Due Date
    PartiallyPaid --> Overdue: Past Due Date
    Overdue --> Paid: Full Payment
```
| Status | Description |
|--------|-------------|
| `Pending` | Bill generated, no payments received |
| `PartiallyPaid` | Some amount paid, balance remaining |
| `Paid` | Fully settled |
| `Overdue` | Past due date without full payment |
### 7.3 Payment Status
```mermaid
stateDiagram-v2
    [*] --> Initiated: Payment Created
    Initiated --> Processing: OTP Verified
    Processing --> Paid: Saga Completed
    Processing --> Failed: Saga Failed (no compensation)
    Processing --> Compensated: Saga Failed (compensation applied)
```
| Status | Description |
|--------|-------------|
| `Initiated` | Payment record created, OTP sent |
| `Processing` | OTP verified, saga executing |
| `Paid` | All saga steps completed successfully |
| `Failed` | Saga failed, no steps to compensate |
| `Compensated` | Saga failed, all completed steps reversed |
---
## 8. Business Rules
### 8.1 User Rules
- Email must be unique across all users
- Password minimum: 8 characters, 1 uppercase, 1 lowercase, 1 digit
- OTP expires after 10 minutes (5 minutes for payment OTP)
- Suspended users cannot login or perform any action
- Google SSO users have null PasswordHash
### 8.2 Card Rules
- Card number encrypted before storage (AES)
- Only last 4 digits stored in plain text
- Soft delete (IsDeleted flag) — no physical deletion
- Deleted cards excluded from all queries
- One card can be marked as default; setting a new default unsets the previous
### 8.3 Billing Rules
- Minimum due calculated as percentage of total bill amount
- Bills transition to Overdue if not paid by due date
- Partial payments allowed (status → PartiallyPaid)
- Statements aggregate all transactions within billing period
### 8.4 Rewards Rules
- Reward rate determined by tier (network + issuer specific)
- Rewards earned on payment completion
- Rewards redeemable during payment (reduces payment amount)
- Reward redemption is reversible via saga compensation
- Each user has exactly one reward account
### 8.5 Payment Rules
- Payments require OTP verification (2FA)
- Payment via Wallet or Card
- Wallet balance must be sufficient for wallet payments
- Payment OTP expires after 5 minutes
- Expired payments cleaned up by background job
- All payments go through saga orchestration
### 8.6 Wallet Rules
- Each user has exactly one wallet (auto-created on first use)
- Wallet balance cannot go negative
- Wallet transactions are immutable
- Razorpay top-ups verified via HMAC-SHA256 signature
- Duplicate webhooks handled idempotently
### 8.7 Saga Rules
- Saga is idempotent (same CorrelationId = same saga instance)
- Compensation triggered on any step failure
- Compensation executes in reverse order
- Saga state persisted for recovery
- 30-second timeout per step triggers compensation
---
*End of Low-Level Design Document*