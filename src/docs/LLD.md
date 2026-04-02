# CredVault — Low-Level Design (LLD)

> Version: 1.0 | Date: April 3, 2026  
> Stack: .NET 10 (Backend) · Angular 21 (Frontend) · SQL Server · RabbitMQ · MassTransit · EF Core

---

## Table of Contents

1. [Solution Structure](#1-solution-structure)
2. [Shared Contracts Library](#2-shared-contracts-library)
3. [Identity Service — Internal Design](#3-identity-service--internal-design)
4. [Card Service — Internal Design](#4-card-service--internal-design)
5. [Billing Service — Internal Design](#5-billing-service--internal-design)
6. [Payment Service — Internal Design](#6-payment-service--internal-design)
7. [Payment SAGA — State Machine Deep Dive](#7-payment-saga--state-machine-deep-dive)
8. [Notification Service — Internal Design](#8-notification-service--internal-design)
9. [API Gateway — Routing Design](#9-api-gateway--routing-design)
10. [Frontend Architecture — Angular](#10-frontend-architecture--angular)
11. [Cross-Cutting Concerns](#11-cross-cutting-concerns)
12. [Database Schema — All Tables](#12-database-schema--all-tables)
13. [Complete Event Catalog](#13-complete-event-catalog)
14. [Request/Response Contracts](#14-requestresponse-contracts)

---

## 1. Solution Structure

```
server/
├── shared.contracts/
│   └── Shared.Contracts/
│       ├── Controllers/        BaseApiController.cs
│       ├── DTOs/               OperationResult.cs, Card/, Identity/, Payment/
│       ├── Enums/              CardNetwork.cs
│       ├── Events/             Billing/, Card/, Identity/, Payment/, Saga/
│       ├── Extensions/         ServiceCollectionExtensions.cs
│       ├── Middleware/         ExceptionHandlingMiddleware.cs
│       └── Models/             ApiResponse.cs
│
├── services/
│   ├── identity-service/
│   │   ├── IdentityService.API/         Controllers/, Program.cs
│   │   ├── IdentityService.Application/ Commands/, Queries/, Abstractions/
│   │   ├── IdentityService.Domain/      Entities/, Enums/
│   │   └── IdentityService.Infrastructure/ Persistence/
│   │
│   ├── card-service/
│   │   ├── CardService.API/             Controllers/, Messaging/, Program.cs
│   │   ├── CardService.Application/     Commands/, Queries/, Common/
│   │   ├── CardService.Domain/          Entities/
│   │   └── CardService.Infrastructure/  Persistence/
│   │
│   ├── billing-service/
│   │   ├── BillingService.API/          Controllers/, Messaging/, Program.cs
│   │   ├── BillingService.Application/  Commands/, Queries/
│   │   ├── BillingService.Domain/       Entities/ (includes Enums, BillingCycleCalculator)
│   │   └── BillingService.Infrastructure/ Persistence/
│   │
│   ├── payment-service/
│   │   ├── PaymentService.API/          Controllers/, Program.cs
│   │   ├── PaymentService.Application/  Commands/, Queries/, Sagas/, Validators/
│   │   ├── PaymentService.Domain/       Entities/, Enums/, Interfaces/
│   │   └── PaymentService.Infrastructure/ Messaging/Consumers/, Persistence/, Migrations/
│   │
│   ├── notification-service/
│   │   ├── NotificationService.API/     Controllers/, Program.cs
│   │   ├── NotificationService.Application/ Commands/, Queries/
│   │   ├── NotificationService.Domain/  Entities/
│   │   └── NotificationService.Infrastructure/ Persistence/
│   │
│   └── gateway/
│       └── Gateway.API/                 Program.cs, ocelot.json

client/src/app/
├── core/
│   ├── guards/         auth.guard.ts, admin.guard.ts, guest.guard.ts
│   ├── interceptors/   auth.interceptor.ts
│   ├── models/         auth.models.ts, card.models.ts
│   └── services/       auth, dashboard, payment, billing, rewards, admin
├── features/
│   ├── auth/           login, register, verify, forgot-password, reset-password
│   ├── dashboard/      dashboard.component
│   ├── cards/          card-details.component
│   ├── bills/          bills.component
│   ├── payments/       payments.component
│   ├── statements/     statements.component, statement-detail.component
│   ├── rewards/        rewards.component
│   ├── notifications/  notifications.component
│   ├── profile/        profile.component
│   └── admin/          admin-layout, dashboard, users, issuers, bills,
│                       rewards, logs, violations
└── shared/
    └── components/     (empty — shared components not yet extracted)
```

Each backend service follows Clean Architecture with 4 layers:
- **Domain** — entities, enums, domain logic (no dependencies)
- **Application** — CQRS commands/queries via MediatR, business rules
- **Infrastructure** — EF Core DbContext, repositories, migrations, messaging consumers
- **API** — ASP.NET controllers, messaging consumers that call Application layer, Program.cs bootstrap

---

## 2. Shared Contracts Library

All services reference `Shared.Contracts` — a class library that provides common building blocks.

### 2.1 ApiResponse\<T\>

```csharp
public class ApiResponse<T>
{
    public bool   Success  { get; set; }
    public string Message  { get; set; }
    public T?     Data     { get; set; }
    public string TraceId  { get; set; }  // HttpContext.TraceIdentifier
}
```

Every HTTP response from every service is wrapped in this envelope.

### 2.2 OperationResult

```csharp
public class OperationResult
{
    public bool    Success   { get; set; }
    public string  Message   { get; set; }
    public string? ErrorCode { get; set; }  // drives HTTP status code mapping
    public object? Data      { get; set; }
}
```

Used as the return type from all MediatR command/query handlers.

### 2.3 BaseApiController

All controllers inherit `BaseApiController`. Key methods:

| Method | Purpose |
|---|---|
| `BuildResponse<T>(success, data, message)` | Wraps data in `ApiResponse<T>` |
| `CreateResponse<T>(success, data, message, errorCode, statusCode)` | Maps `errorCode` → HTTP status |
| `GetUserIdFromToken()` | Extracts `Guid` from `ClaimTypes.NameIdentifier` |
| `BadRequestResponse(message)` | Returns 400 with `ApiResponse` |
| `NotFoundResponse(message)` | Returns 404 with `ApiResponse` |
| `UnauthorizedResponse(message)` | Returns 401 with `ApiResponse` |

ErrorCode → HTTP status mapping in `CreateResponse`:

```
"ValidationError"                → 400 BadRequest
"NotFound" / "UserNotFound" / "CardNotFound" → 404 NotFound
"Conflict" / "DuplicateEmail"    → 409 Conflict
"Unauthorized" / "InvalidCredentials" → 401 Unauthorized
"Forbidden" / "AccountLocked"   → 403 Forbidden
"ServiceUnavailable"             → 503 ServiceUnavailable
(default)                        → 400 BadRequest
```

### 2.4 ExceptionHandlingMiddleware

Registered in every service's `Program.cs`. Catches all unhandled exceptions and returns:

```json
{
  "success": false,
  "message": "Unhandled exception",
  "data": {
    "title": "Unhandled Server Error",
    "detail": "An unexpected error occurred...",
    "status": 500
  },
  "traceId": "..."
}
```

### 2.5 ServiceCollectionExtensions

Shared extension methods registered in every service:

| Method | What it does |
|---|---|
| `AddStandardAuth(config)` | Registers JWT Bearer auth with `JwtOptions` from config |
| `AddStandardCors()` | Adds `AllowWebClients` CORS policy (AllowAnyOrigin/Header/Method) |
| `AddStandardApi()` | Registers OpenAPI + ProblemDetails |
| `AddStandardMessaging(config, configure, serviceName)` | Registers MassTransit with RabbitMQ, kebab-case formatter, service-prefixed queues |
| `UseStandardApi(app, title)` | Maps OpenAPI + Scalar UI in Development |

### 2.6 JwtOptions

```csharp
public class JwtOptions
{
    public string SecretKey          { get; set; }  // min 32 chars
    public string Issuer             { get; set; }  // "IdentityService"
    public string Audience           { get; set; }  // "CredVaultClients"
    public int    AccessTokenMinutes { get; set; }  // 30
}
```

JWT validation parameters: `ValidateIssuer=true`, `ValidateAudience=true`, `ValidateLifetime=true`, `ClockSkew=30s`.

### 2.7 CardNetwork Enum

```csharp
public enum CardNetwork { Unknown = 0, Visa = 1, Mastercard = 2 }
```

Shared across Card, Billing, and Payment services.

---

## 3. Identity Service — Internal Design

**Port:** 5001 | **Database:** `credvault_identity`

### 3.1 Domain Entity — IdentityUser

```csharp
public sealed class IdentityUser
{
    public Guid       Id                              { get; set; }
    public string     Email                           { get; set; }
    public string     FullName                        { get; set; }
    public string     PasswordHash                    { get; set; }  // BCrypt
    public bool       IsEmailVerified                 { get; set; }
    public string?    EmailVerificationOtp            { get; set; }  // 6-digit
    public DateTime?  EmailVerificationOtpExpiresAtUtc{ get; set; }
    public string?    PasswordResetOtp                { get; set; }  // 6-digit
    public DateTime?  PasswordResetOtpExpiresAtUtc    { get; set; }
    public UserStatus Status                          { get; set; }  // enum
    public UserRole   Role                            { get; set; }  // enum
    public DateTime   CreatedAtUtc                    { get; set; }
    public DateTime   UpdatedAtUtc                    { get; set; }
}
```

### 3.2 Enums

```csharp
public enum UserStatus { PendingVerification=1, Active=2, Suspended=3, Blocked=4, Deleted=5 }
public enum UserRole   { User=1, Admin=2 }
```

### 3.3 Controllers

#### AuthController — `[Route("api/v1/identity/auth")]`

| Method | Route | Auth | Command Dispatched |
|---|---|---|---|
| POST | `/register` | None | `RegisterCommand(email, password, fullName)` |
| POST | `/login` | None | `LoginCommand(email, password)` |
| POST | `/verify-email-otp` | None | `VerifyEmailOtpCommand(email, otp)` |
| POST | `/resend-verification` | None | `ResendVerificationCommand(email)` |
| POST | `/forgot-password` | None | `ForgotPasswordCommand(email)` |
| POST | `/reset-password` | None | `ResetPasswordCommand(email, otp, newPassword)` |

All responses go through `CreateResponse()` which maps `ErrorCode` to HTTP status.

#### UsersController — `[Route("api/v1/identity/users")]`

| Method | Route | Auth | Role | Action |
|---|---|---|---|---|
| GET | `/me` | JWT | any | `GetCurrentUserQuery(userId)` |
| PUT | `/me` | JWT | any | `UpdateProfileCommand(userId, fullName)` |
| PUT | `/me/password` | JWT | any | `ChangePasswordCommand(userId, currentPwd, newPwd)` |
| GET | `/{userId}` | JWT | admin | `GetUserByIdQuery(userId)` |
| PUT | `/{userId}/status` | JWT | admin | `UpdateUserStatusCommand(userId, status)` |
| GET | `/stats` | JWT | admin | `GetUserStatsQuery()` |
| GET | `/` | JWT | admin | `GetAllUsersQuery(page, pageSize, search, status)` |

### 3.4 Application Layer — Commands

**RegisterCommand:**
1. Validate email uniqueness (query DB)
2. Hash password with BCrypt
3. Generate 6-digit OTP, set expiry (+15 min)
4. Create `IdentityUser` with `Status=PendingVerification`
5. Publish `IUserRegistered` event via MassTransit
6. Return `OperationResult { Success=true }`

**LoginCommand:**
1. Find user by email → `ErrorCode="UserNotFound"` if missing
2. Verify BCrypt hash → `ErrorCode="InvalidCredentials"` if mismatch
3. Check `Status == Active` → `ErrorCode="AccountLocked"` if not
4. Generate JWT with claims: `NameIdentifier=userId`, `email`, `role`
5. Return `{ accessToken, user }`

**VerifyEmailOtpCommand:**
1. Find user by email
2. Check OTP matches and not expired
3. Set `IsEmailVerified=true`, `Status=Active`
4. Clear OTP fields
5. Generate and return JWT (auto-login after verification)

**ForgotPasswordCommand:**
1. Find user by email (silently succeed if not found — no enumeration)
2. Generate 6-digit OTP, set expiry (+15 min)
3. Publish `IUserOtpGenerated` event with `Purpose="PasswordReset"`

**ResetPasswordCommand:**
1. Find user by email
2. Validate OTP and expiry
3. Hash new password
4. Clear OTP fields, update `UpdatedAtUtc`

### 3.5 Events Published

```csharp
// On register
IUserRegistered { UserId, Email, FullName, CreatedAtUtc }

// On forgot-password / resend-verification
IUserOtpGenerated { UserId, Email, FullName, OtpCode, Purpose, ExpiresAtUtc }
```

Both route to `notification-domain-event` queue → NotificationService.

---

## 4. Card Service — Internal Design

**Port:** 5002 | **Database:** `credvault_cards`

### 4.1 Domain Entities

#### CreditCard

```csharp
public sealed class CreditCard
{
    public Guid     Id                  { get; set; }
    public Guid     UserId              { get; set; }
    public Guid     IssuerId            { get; set; }
    public CardIssuer? Issuer           { get; set; }  // nav property
    public string   CardholderName      { get; set; }
    public int      ExpMonth            { get; set; }
    public int      ExpYear             { get; set; }
    public string   Last4               { get; set; }  // last 4 digits
    public string   MaskedNumber        { get; set; }  // e.g. "4111 **** **** 1111"
    public decimal  CreditLimit         { get; set; }
    public decimal  OutstandingBalance  { get; set; }
    public int      BillingCycleStartDay{ get; set; }  // 1–28
    public int      StrikeCount         { get; set; }  // default 0
    public bool     IsBlocked           { get; set; }
    public DateTime? BlockedAtUtc       { get; set; }
    public DateTime? UnblockedAtUtc     { get; set; }
    public bool     IsDefault           { get; set; }
    public bool     IsVerified          { get; set; }
    public bool     IsDeleted           { get; set; }  // soft delete
    public DateTime? DeletedAtUtc       { get; set; }
    public DateTime CreatedAtUtc        { get; set; }
    public DateTime UpdatedAtUtc        { get; set; }

    // Domain methods
    public void AddStrike()    // increments StrikeCount; blocks at >= 3
    public void ClearStrikes() // resets StrikeCount=0, IsBlocked=false
}
```

`AddStrike()` and `ClearStrikes()` are the only domain methods — all other logic lives in Application commands.

#### CardIssuer

```csharp
public sealed class CardIssuer
{
    public Guid        Id          { get; set; }
    public string      Name        { get; set; }
    public CardNetwork Network     { get; set; }  // Visa=1, Mastercard=2
    public DateTime    CreatedAtUtc{ get; set; }
    public DateTime    UpdatedAtUtc{ get; set; }
}
```

#### CardTransaction

```csharp
public class CardTransaction
{
    public Guid            Id          { get; set; }
    public Guid            CardId      { get; set; }
    public Guid            UserId      { get; set; }
    public TransactionType Type        { get; set; }  // Purchase=1, Payment=2, Refund=3
    public decimal         Amount      { get; set; }
    public string          Description { get; set; }
    public DateTime        DateUtc     { get; set; }
}
```

#### CardViolation

```csharp
public class CardViolation
{
    public Guid          Id            { get; set; }
    public Guid          CardId        { get; set; }
    public Guid          UserId        { get; set; }
    public Guid?         BillId        { get; set; }
    public ViolationType Type          { get; set; }  // LatePayment=1, OverdueBill=2, MissedPayment=3
    public int           StrikeCount   { get; set; }
    public string        Reason        { get; set; }
    public decimal       PenaltyAmount { get; set; }
    public bool          IsActive      { get; set; }
    public DateTime      AppliedAtUtc  { get; set; }
    public DateTime?     ClearedAtUtc  { get; set; }
}
```

### 4.2 Controllers

#### CardsController — `[Route("api/v1/cards")]`

| Method | Route | Auth | Role | Description |
|---|---|---|---|---|
| POST | `/` | JWT | any | Create card — validates number, detects network, checks duplicates |
| GET | `/` | JWT | any | List user's cards ordered by IsDefault DESC, UpdatedAt DESC |
| GET | `/transactions` | JWT | any | All transactions for authenticated user |
| GET | `/{cardId}` | JWT | any | Get single card (ownership enforced) |
| PUT | `/{cardId}` | JWT | any | Update cardholderName, expiry, isDefault |
| DELETE | `/{cardId}` | JWT | any | Soft delete (IsDeleted=true) |
| GET | `/{cardId}/transactions` | JWT | any | Transactions for specific card |
| POST | `/{cardId}/transactions` | JWT | any | Add transaction |
| GET | `/{cardId}/health` | JWT | any | Health score calculation |
| GET | `/admin/{cardId}` | JWT | admin | Admin view — no user restriction |
| PUT | `/{cardId}/admin` | JWT | admin | Set CreditLimit, OutstandingBalance, BillingCycleStartDay |
| GET | `/user/{userId}` | JWT | admin | All cards for a user |

#### IssuersController — `[Route("api/v1/issuers")]`

| Method | Route | Auth | Role |
|---|---|---|---|
| GET | `/` | JWT | any |
| GET | `/{id}` | JWT | any |
| POST | `/` | JWT | admin |
| PUT | `/{id}` | JWT | admin |
| DELETE | `/{id}` | JWT | admin |

#### ViolationsController — `[Route("api/v1/cards/admin")]`

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/blocked` | JWT | List all blocked cards |
| POST | `/{cardId}/unblock` | JWT | Unblock card, reset strikes |
| GET | `/{cardId}/violations` | JWT | Get violation history |
| POST | `/{cardId}/violations/clear` | JWT | Clear all active violations |

### 4.3 Card Health Score Algorithm

```
utilization = OutstandingBalance / CreditLimit

healthScore = 700 + (1 - utilization) * 200
if utilization > 0.80: healthScore -= 100
healthScore = clamp(healthScore, 300, 1000)

grade:
  >= 800 → "Excellent"
  >= 700 → "Good"
  >= 500 → "Fair"
  <  500 → "Poor"
```

### 4.4 Messaging Consumers (RabbitMQ)

| Consumer | Event | Action |
|---|---|---|
| `CardDeductionSagaConsumer` | `ICardDeductionRequested` | Idempotency check → deduct `OutstandingBalance` → create `CardTransaction(Payment)` → publish `ICardDeductionSucceeded/Failed` |
| `BillOverdueConsumer` | `IBillOverdueDetected` | `ApplyStrikeCommand(cardId, billId, reason)` → `card.AddStrike()` → if blocked, publish `ICardBlocked` |
| `BillPaidConsumer` | `IBillUpdateSucceeded` | `ClearStrikesCommand(cardId)` → `card.ClearStrikes()` |
| `PaymentReversedConsumer` | `IPaymentReversed` | `RefundCardBalanceCommand` → add back to `OutstandingBalance` |
| `UserDeletedConsumer` | `IUserDeleted` | Soft-delete all cards for user |

**Idempotency in CardDeductionSagaConsumer:**
Before deducting, checks if a `CardTransaction` with `Description = "Saga:{CorrelationId}"` already exists. If yes, publishes success immediately without re-processing.

### 4.5 Events Published

```csharp
ICardAdded { CardId, UserId, Email, FullName, CardNumberLast4, CardHolderName, AddedAt }
```

---

## 5. Billing Service — Internal Design

**Port:** 5003 | **Database:** `credvault_billing`

### 5.1 Domain Entities

#### Bill

```csharp
public sealed class Bill
{
    public Guid        Id             { get; set; }
    public Guid        UserId         { get; set; }
    public Guid        CardId         { get; set; }  // cross-service ref, no FK
    public CardNetwork CardNetwork    { get; set; }
    public Guid        IssuerId       { get; set; }
    public decimal     Amount         { get; set; }
    public decimal     MinDue         { get; set; }
    public string      Currency       { get; set; }  // default "USD"
    public DateTime    BillingDateUtc { get; set; }
    public DateTime    DueDateUtc     { get; set; }
    public decimal?    AmountPaid     { get; set; }
    public DateTime?   PaidAtUtc      { get; set; }
    public BillStatus  Status         { get; set; }
    public DateTime    CreatedAtUtc   { get; set; }
    public DateTime    UpdatedAtUtc   { get; set; }
}

public enum BillStatus { Pending=1, Paid=2, Overdue=3, Cancelled=4, PartiallyPaid=5 }
```

#### RewardTier

```csharp
public sealed class RewardTier
{
    public Guid        Id               { get; set; }
    public CardNetwork CardNetwork      { get; set; }
    public Guid?       IssuerId         { get; set; }  // null = network-wide
    public decimal     MinSpend         { get; set; }
    public decimal     RewardRate       { get; set; }  // e.g. 0.015 = 1.5%
    public DateTime    EffectiveFromUtc { get; set; }
    public DateTime?   EffectiveToUtc   { get; set; }
    public DateTime    CreatedAtUtc     { get; set; }
    public DateTime    UpdatedAtUtc     { get; set; }
}
```

#### RewardAccount

```csharp
public sealed class RewardAccount
{
    public Guid    Id             { get; set; }
    public Guid    UserId         { get; set; }
    public Guid    RewardTierId   { get; set; }
    public decimal PointsBalance  { get; set; }
    public DateTime CreatedAtUtc  { get; set; }
    public DateTime UpdatedAtUtc  { get; set; }
}

public enum RewardTransactionType { Earned=1, Adjusted=2, Redeemed=3, Reversed=4 }
```

#### Statement

```csharp
public class Statement
{
    public Guid            Id               { get; set; }
    public Guid            UserId           { get; set; }
    public Guid            CardId           { get; set; }
    public Guid?           BillId           { get; set; }
    public string          StatementPeriod  { get; set; }  // e.g. "Mar 2026"
    public DateTime        PeriodStartUtc   { get; set; }
    public DateTime        PeriodEndUtc     { get; set; }
    public DateTime        GeneratedAtUtc   { get; set; }
    public DateTime?       DueDateUtc       { get; set; }
    public decimal         OpeningBalance   { get; set; }
    public decimal         TotalPurchases   { get; set; }
    public decimal         TotalPayments    { get; set; }
    public decimal         TotalRefunds     { get; set; }
    public decimal         PenaltyCharges   { get; set; }
    public decimal         InterestCharges  { get; set; }
    public decimal         ClosingBalance   { get; set; }
    public decimal         MinimumDue       { get; set; }
    public decimal         AmountPaid       { get; set; }
    public DateTime?       PaidAtUtc        { get; set; }
    public StatementStatus Status           { get; set; }
    public string          CardLast4        { get; set; }
    public string          CardNetwork      { get; set; }
    public string          IssuerName       { get; set; }
    public decimal         CreditLimit      { get; set; }
    public decimal         AvailableCredit  { get; set; }
    public string?         Notes            { get; set; }
    public ICollection<StatementTransaction> Transactions { get; set; }
}

public enum StatementStatus { Generated=1, Paid=2, Overdue=3, PartiallyPaid=4 }
```

### 5.2 BillingCycleCalculator (Static Utility)

```csharp
public static class BillingCycleCalculator
{
    public const int     GracePeriodDays    = 12;
    public const decimal MonthlyInterestRate = 0.035m;  // 3.5%

    // Returns billing period start date based on card's BillingCycleStartDay
    public static DateTime GetCurrentBillingPeriodStart(int billingCycleStartDay)

    // Adds GracePeriodDays to billing date
    public static DateTime CalculateDueDate(DateTime billingDate)

    // Returns (billingDate, dueDate) tuple
    public static (DateTime, DateTime) CalculateBillingAndDueDate(int billingCycleStartDay)

    // Compound interest: overdueAmount * 0.035 * overdueMonths
    public static decimal CalculateMonthlyInterest(decimal overdueAmount, int overdueMonths)
}
```

### 5.3 Controllers

#### BillsController — `[Route("api/v1/billing/bills")]`

| Method | Route | Auth | Role | Description |
|---|---|---|---|---|
| GET | `/` | JWT | any | List user's bills |
| GET | `/{billId}` | JWT | any | Get single bill |
| POST | `/admin/generate-bill` | JWT | admin | Generate bill for user/card |
| POST | `/admin/check-overdue` | JWT | admin | Detect and flag overdue bills |

#### RewardsController — `[Route("api/v1/billing/rewards")]`

| Method | Route | Auth | Role |
|---|---|---|---|
| GET | `/tiers` | JWT | any |
| POST | `/tiers` | JWT | admin |
| DELETE | `/tiers/{id}` | JWT | admin |
| GET | `/account` | JWT | any |
| GET | `/transactions` | JWT | any |

#### StatementsController — `[Route("api/v1/billing/statements")]`

| Method | Route | Auth | Role |
|---|---|---|---|
| GET | `/` | JWT | any |
| GET | `/{statementId}` | JWT | any |
| POST | `/generate` | JWT | any |
| POST | `/admin/generate` | JWT | admin |
| GET | `/admin/all` | JWT | admin |
| GET | `/bill/{billId}` | JWT | any |

### 5.4 MarkBillPaidCommand — Business Logic

Called by `BillUpdateSagaConsumer` when SAGA reaches AwaitingBillUpdate:

1. Find bill by `billId` and `userId` → fail if not found
2. If already `Paid` → still process rewards (idempotency)
3. Validate `amount >= MinDue`
4. Set `Bill.Status = Paid`, `AmountPaid = amount`, `PaidAtUtc = now`
5. Find applicable `RewardTier` (match by `CardNetwork`, `IssuerId`, date range, `MinSpend`)
6. Calculate points: `points = amount * rewardRate`
7. Upsert `RewardAccount` for user, add points to `PointsBalance`
8. Create `RewardTransaction(Type=Earned, Points=points, BillId=billId)`
9. Auto-generate `Statement` with period breakdown
10. Publish `IBillUpdateSucceeded`

### 5.5 Messaging Consumers

| Consumer | Event | Action |
|---|---|---|
| `BillUpdateSagaConsumer` | `IBillUpdateRequested` | Calls `MarkBillPaidCommand` → publishes `IBillUpdateSucceeded/Failed` |
| `RevertBillSagaConsumer` | `IRevertBillUpdateRequested` | Calls `RevertBillPaidCommand` → reverts bill to Pending, deducts reward points → publishes `IRevertBillUpdateSucceeded/Failed` |
| `UserDeletedConsumer` | `IUserDeleted` | Cancels all bills, removes reward account |

### 5.6 Events Published

```csharp
IBillGenerated { BillId, UserId, Email, FullName, CardId, Amount, DueDate, GeneratedAt }
IBillOverdueDetected { BillId, CardId, UserId, OverdueAmount, DueDate, DaysOverdue, DetectedAt }
```

---

## 6. Payment Service — Internal Design

**Port:** 5004 | **Database:** `credvault_payments`

### 6.1 Domain Entities

#### Payment

```csharp
public sealed class Payment
{
    public Guid          Id             { get; set; }
    public Guid          UserId         { get; set; }
    public Guid          CardId         { get; set; }
    public Guid          BillId         { get; set; }
    public decimal       Amount         { get; set; }
    public PaymentType   PaymentType    { get; set; }
    public PaymentStatus Status         { get; set; }
    public string?       FailureReason  { get; set; }
    public string?       OtpCode        { get; set; }
    public DateTime?     OtpExpiresAtUtc{ get; set; }
    public bool          IsDeleted      { get; set; }
    public DateTime?     DeletedAtUtc   { get; set; }
    public DateTime      CreatedAtUtc   { get; set; }
    public DateTime      UpdatedAtUtc   { get; set; }
    public ICollection<Transaction> Transactions { get; set; }
}

public enum PaymentStatus  { Initiated, Processing, Completed, Failed, Reversed, Cancelled }
public enum PaymentType    { Full, Partial, Scheduled }
```

#### Transaction

```csharp
public sealed class Transaction
{
    public Guid            Id          { get; set; }
    public Guid            PaymentId   { get; set; }
    public Guid            UserId      { get; set; }
    public decimal         Amount      { get; set; }
    public TransactionType Type        { get; set; }  // Payment, Reversal
    public string          Description { get; set; }
    public DateTime        CreatedAtUtc{ get; set; }
    public Payment         Payment     { get; set; }  // nav property
}
```

#### PaymentOrchestrationSagaState

```csharp
public sealed class PaymentOrchestrationSagaState : SagaStateMachineInstance
{
    public Guid     CorrelationId       { get; set; }  // = PaymentId
    public string   CurrentState        { get; set; }  // state machine state
    public Guid     PaymentId           { get; set; }
    public Guid     UserId              { get; set; }
    public string?  Email               { get; set; }
    public string?  FullName            { get; set; }
    public Guid     CardId              { get; set; }
    public Guid     BillId              { get; set; }
    public decimal  Amount              { get; set; }
    public string?  PaymentType         { get; set; }
    public string?  OtpCode             { get; set; }
    public DateTime? OtpExpiresAtUtc    { get; set; }
    public bool     OtpVerified         { get; set; }
    public bool     PaymentProcessed    { get; set; }
    public bool     BillUpdated         { get; set; }
    public bool     CardDeducted        { get; set; }
    public string?  PaymentError        { get; set; }
    public string?  BillUpdateError     { get; set; }
    public string?  CardDeductionError  { get; set; }
    public string?  CompensationReason  { get; set; }
    public int      CompensationAttempts{ get; set; }
    public DateTime CreatedAtUtc        { get; set; }
    public DateTime UpdatedAtUtc        { get; set; }
}
```

### 6.2 PaymentsController — `[Route("api/v1/payments")]`

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/initiate` | JWT | Validate bill, generate OTP, create Payment, start SAGA |
| POST | `/{paymentId}/verify-otp` | JWT | Validate OTP, publish `IOtpVerified` |
| GET | `/` | JWT | List all payments for user |
| GET | `/{paymentId}` | JWT | Get single payment (ownership check) |
| GET | `/{paymentId}/transactions` | JWT | Get payment transactions |

### 6.3 InitiatePaymentCommand — Step by Step

1. Parse `PaymentType` enum from string (case-insensitive)
2. Fetch bill from BillingService via HTTP (`Authorization` header forwarded)
3. Validate bill belongs to user (IDOR check)
4. Generate 6-digit OTP: `Random.Shared.Next(100000, 999999).ToString()`
5. Create `Payment` record with `Status=Initiated`, store OTP + expiry
6. Publish `IStartPaymentOrchestration` (starts SAGA)
7. Publish `IPaymentOtpGenerated` (triggers OTP email via NotificationService)
8. Return `{ PaymentId, OtpRequired=true, Status="Pending OTP Verification" }`

### 6.4 VerifyOtpCommand — Step by Step

1. Find payment by `paymentId`
2. Verify `payment.UserId == userId` (ownership)
3. Verify `payment.Status == Initiated`
4. Verify `payment.OtpCode == otpCode`
5. Verify `payment.OtpExpiresAtUtc > DateTime.UtcNow`
6. Publish `IOtpVerified { CorrelationId=paymentId, OtpCode, VerifiedAt }`
7. SAGA picks up `IOtpVerified` and transitions to `AwaitingPaymentConfirmation`

### 6.5 Infrastructure Messaging Consumers

| Consumer | Event | Action |
|---|---|---|
| `PaymentProcessConsumer` | `IPaymentProcessRequested` | Set `Payment.Status=Processing` → publish `IPaymentProcessSucceeded` |
| `PaymentCompletedConsumer` | `IPaymentCompleted` | Set `Payment.Status=Completed` → create `Transaction(Payment)` — idempotent |
| `PaymentFailedConsumer` | `IPaymentFailed` | Set `Payment.Status=Failed`, set `FailureReason` — idempotent |
| `RevertPaymentConsumer` | `IRevertPaymentRequested` | Set `Payment.Status=Reversed` → create `Transaction(Reversal)` → publish `IPaymentReversed` |
| `UserDeletedConsumer` | `IUserDeleted` | Cancel all `Initiated` payments for user |

---

## 7. Payment SAGA — State Machine Deep Dive

The `PaymentOrchestrationSaga` is a `MassTransitStateMachine<PaymentOrchestrationSagaState>`. State is persisted in the `PaymentOrchestrationSagas` SQL table. `CorrelationId = PaymentId`.

### 7.1 States

| State | Meaning |
|---|---|
| `Initial` | SAGA not yet started |
| `AwaitingOtpVerification` | Waiting for user to submit OTP |
| `AwaitingPaymentConfirmation` | OTP verified, waiting for payment processing |
| `AwaitingBillUpdate` | Payment processed, waiting for bill to be marked paid |
| `AwaitingCardDeduction` | Bill updated, waiting for card balance deduction |
| `Completed` | All steps succeeded — terminal |
| `Compensating` | A step failed, rolling back |
| `Compensated` | Rollback succeeded — terminal |
| `Failed` | Rollback failed or OTP failed — terminal |

### 7.2 Happy Path Transitions

```
IStartPaymentOrchestration
  → stores all context (PaymentId, UserId, CardId, BillId, Amount, OtpCode)
  → OtpExpiresAtUtc = now + 10 min
  → state: AwaitingOtpVerification

IOtpVerified
  → OtpVerified = true
  → publishes IPaymentProcessRequested
  → state: AwaitingPaymentConfirmation

IPaymentProcessSucceeded
  → PaymentProcessed = true
  → publishes IBillUpdateRequested
  → state: AwaitingBillUpdate

IBillUpdateSucceeded
  → BillUpdated = true
  → publishes ICardDeductionRequested
  → state: AwaitingCardDeduction

ICardDeductionSucceeded
  → CardDeducted = true
  → publishes IPaymentCompleted { PaymentId, UserId, Email, FullName, Amount, CompletedAt }
  → state: Completed (terminal)
```

### 7.3 Compensation Paths

**OTP Failed:**
```
IOtpFailed → CompensationReason = reason → state: Failed (terminal)
```

**Payment Process Failed:**
```
IPaymentProcessFailed → PaymentError = reason → state: Failed (terminal)
```

**Bill Update Failed (after payment processed):**
```
IBillUpdateFailed
  → CompensationReason = "Bill update failed: {reason}"
  → publishes IRevertPaymentRequested
  → state: Compensating

IRevertPaymentSucceeded
  → PaymentProcessed = false
  → publishes IPaymentFailed
  → state: Compensated (terminal)

IRevertPaymentFailed (retry up to 5 attempts)
  → CompensationAttempts++
  → if >= 5 → state: Failed
  → else → re-publish IRevertPaymentRequested → stay in Compensating
```

**Card Deduction Failed (after bill updated):**
```
ICardDeductionFailed
  → CardDeductionError = reason
  → publishes IRevertBillUpdateRequested
  → state: Compensating

IRevertBillUpdateSucceeded
  → BillUpdated = false
  → publishes IRevertPaymentRequested (continues compensation chain)

IRevertBillUpdateFailed (retry up to 3 attempts)
  → CompensationAttempts++
  → if >= 3 → state: Failed
  → else → re-publish IRevertBillUpdateRequested → stay in Compensating
```

### 7.4 Idempotency Guards

Terminal states (`Completed`, `Compensated`, `Failed`) use `Ignore()` for all relevant events:

```csharp
During(Completed,
    Ignore(PaymentSucceeded),
    Ignore(BillUpdateSucceeded),
    Ignore(CardDeductionSucceeded)
);
```

This prevents duplicate processing if a message is re-delivered after the SAGA has already finished.

### 7.5 SAGA Event Correlation

All SAGA events correlate by `CorrelationId`:

```csharp
Event(() => OtpVerified, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
```

`CorrelationId` is always equal to `PaymentId`, so the SAGA instance is uniquely identified per payment.

### 7.6 Full SAGA Event List (18 events)

| Category | Events |
|---|---|
| Start | `IStartPaymentOrchestration` |
| OTP | `IOtpVerified`, `IOtpFailed` |
| Payment Process | `IPaymentProcessRequested`, `IPaymentProcessSucceeded`, `IPaymentProcessFailed` |
| Bill Update | `IBillUpdateRequested`, `IBillUpdateSucceeded`, `IBillUpdateFailed` |
| Card Deduction | `ICardDeductionRequested`, `ICardDeductionSucceeded`, `ICardDeductionFailed` |
| Revert Bill | `IRevertBillUpdateRequested`, `IRevertBillUpdateSucceeded`, `IRevertBillUpdateFailed` |
| Revert Payment | `IRevertPaymentRequested`, `IRevertPaymentSucceeded`, `IRevertPaymentFailed` |

---

## 8. Notification Service — Internal Design

**Port:** 5005 | **Database:** `credvault_notifications`

### 8.1 Domain Entities

#### NotificationLog

```
Id              GUID        PK
UserId          GUID        nullable
Recipient       NVARCHAR    email or phone
Type            NVARCHAR    "Email" | "Sms" | "Push"
Subject         NVARCHAR
Body            NVARCHAR
IsSuccess       BIT
ErrorMessage    NVARCHAR    nullable
TraceId         NVARCHAR    nullable
CreatedAtUtc    DATETIME2
```

#### AuditLog

```
Id          GUID        PK
EntityName  NVARCHAR    e.g. "PaymentCompleted", "UserOtpGenerated"
EntityId    NVARCHAR
Action      NVARCHAR    "Consumed" | "Processed" | "Failed"
UserId      NVARCHAR    nullable
Changes     NVARCHAR    JSON blob
TraceId     NVARCHAR    nullable
CreatedAtUtc DATETIME2
```

### 8.2 Controllers — `[Route("api/v1/notifications")]`

| Method | Route | Auth | Query Params | Description |
|---|---|---|---|---|
| GET | `/logs` | JWT | `email`, `page=1`, `pageSize=10` | Paginated notification logs |
| GET | `/audit` | JWT | `traceId`, `page=1`, `pageSize=10` | Paginated audit logs |

Response shape for `/logs`:
```json
{ "total": 42, "page": 1, "pageSize": 10, "logs": [...] }
```

### 8.3 DomainEventConsumer

Single consumer on `notification-domain-event` queue. Handles all domain events:

| Event | Action |
|---|---|
| `IUserRegistered` | Send welcome + OTP email |
| `IUserOtpGenerated` | Send OTP email (purpose-specific subject) |
| `ICardAdded` | Send card confirmation email |
| `IBillGenerated` | Send bill notification email |
| `IPaymentOtpGenerated` | Send payment OTP email |
| `IPaymentCompleted` | Send payment confirmation email |
| `IPaymentFailed` | Send payment failure email |
| `IUserDeleted` | Log only — no email sent |
| `IOtpFailed` | Log only — no email sent |

Each event is processed by a `ProcessNotificationCommandHandler` which:
1. Calls `GmailSmtpEmailSender.SendAsync(to, subject, htmlBody)`
2. Writes `NotificationLog` record (`IsSuccess=true/false`)
3. Writes `AuditLog` record (`Action=Processed/Failed`)

### 8.4 GmailSmtpEmailSender

```
Host:     smtp.gmail.com
Port:     587 (STARTTLS)
Username: configured in appsettings.json
Password: Gmail App Password
```

- Connects on service startup
- Graceful fallback: if not configured, logs warning and skips send
- Returns `(bool Success, string? Error)` tuple
- Sends HTML emails

### 8.5 Retry & DLQ Behavior

- MassTransit retries failed message consumption 3 times (immediate)
- After 3 failures → message moved to `notification-domain-event_error` (DLQ)
- Common failure causes: SMTP not configured, template not found, connection error
- DLQ messages can be requeued via RabbitMQ Management UI at `http://localhost:15672`

---

## 9. API Gateway — Routing Design

**Port:** 5006 | **Framework:** Ocelot

### 9.1 Route Configuration

All routes defined in `ocelot.json`. Pattern: `DownstreamPathTemplate` mirrors `UpstreamPathTemplate`.

| Upstream (client calls) | Downstream (gateway forwards to) |
|---|---|
| `/api/v1/identity/{everything}` | `http://localhost:5001/api/v1/identity/{everything}` |
| `/api/v1/cards` | `http://localhost:5002/api/v1/cards` |
| `/api/v1/cards/transactions` | `http://localhost:5002/api/v1/cards/transactions` |
| `/api/v1/cards/{everything}` | `http://localhost:5002/api/v1/cards/{everything}` |
| `/api/v1/issuers` | `http://localhost:5002/api/v1/issuers` |
| `/api/v1/issuers/{everything}` | `http://localhost:5002/api/v1/issuers/{everything}` |
| `/api/v1/billing/bills` | `http://localhost:5003/api/v1/billing/bills` |
| `/api/v1/billing/bills/{everything}` | `http://localhost:5003/api/v1/billing/bills/{everything}` |
| `/api/v1/billing/rewards` | `http://localhost:5003/api/v1/billing/rewards` |
| `/api/v1/billing/rewards/{everything}` | `http://localhost:5003/api/v1/billing/rewards/{everything}` |
| `/api/v1/billing/statements` | `http://localhost:5003/api/v1/billing/statements` |
| `/api/v1/billing/statements/{everything}` | `http://localhost:5003/api/v1/billing/statements/{everything}` |
| `/api/v1/payments` | `http://localhost:5004/api/v1/payments` |
| `/api/v1/payments/{everything}` | `http://localhost:5004/api/v1/payments/{everything}` |
| `/api/v1/notifications` | `http://localhost:5005/api/v1/notifications` |
| `/api/v1/notifications/{everything}` | `http://localhost:5005/api/v1/notifications/{everything}` |

All routes allow methods: `GET, POST, PUT, PATCH, DELETE, OPTIONS`.

### 9.2 Gateway Behavior

- No authentication at gateway level — JWT validation is done by each downstream service
- CORS handled at gateway via `AddStandardCors()` (`AllowAnyOrigin`)
- Health endpoint: `GET /health` → `{ status: "ok", service: "gateway" }`
- `Authorization` header is forwarded as-is to downstream services

### 9.3 Inter-Service HTTP Calls (not via gateway)

Some services call each other directly (bypassing the gateway):

| Caller | Callee | Purpose |
|---|---|---|
| PaymentService | BillingService (`:5003`) | Fetch bill details for IDOR validation |
| PaymentService | IdentityService (`:5001`) | Fetch user email/fullName for SAGA context |
| CardService | IdentityService (`:5001`) | Fetch user email/fullName for `ICardAdded` event |

These use `IHttpClientFactory` with named clients configured in each service's `Program.cs`.

---

## 10. Frontend Architecture — Angular

### 10.1 Application Bootstrap

```typescript
// app.config.ts
export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes,
      withViewTransitions({ skipInitialTransition: true }),
      withInMemoryScrolling({ scrollPositionRestoration: 'enabled' })
    ),
    provideHttpClient(
      withFetch(),
      withInterceptors([authInterceptor])
    )
  ]
};
```

- All components are standalone (no NgModules)
- All routes are lazy-loaded via `loadComponent()`
- `withFetch()` uses the Fetch API instead of XHR
- Single interceptor: `authInterceptor`

### 10.2 Route Guards

#### authGuard
```typescript
// Checks: authService.token() !== null
// On fail: navigate('/login')
```

#### adminGuard
```typescript
// Checks: currentUser.role === 'admin'
// On fail: navigate('/dashboard')
```

#### guestGuard
```typescript
// Checks: authService.token() === null
// On fail: navigate('/dashboard')  (already logged in)
```

Guards are applied as `canActivate` arrays on route definitions. Admin routes use `[authGuard, adminGuard]` — both must pass.

### 10.3 Auth Interceptor

```typescript
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = authService.token();
  if (token) {
    req = req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
  }
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) authService.logout();  // auto-logout on 401
      return throwError(() => error);
    })
  );
};
```

### 10.4 AuthService — Signal-Based State

```typescript
currentUser = signal<User | null>(this.getStoredUser());
token       = signal<string | null>(this.getStoredToken());
pendingEmail = signal<string | null>(this.getPendingEmail());
```

Storage keys in `sessionStorage`:
- `cv_token` — JWT access token
- `cv_user` — serialized `User` object
- `cv_pending_email` — email awaiting OTP verification

`handleAuthSuccess(data)` normalizes both `accessToken` and `AccessToken` casing from backend response.

### 10.5 Data Models

#### User (auth.models.ts)
```typescript
interface User {
  id: string; email: string; fullName: string;
  role: string; status?: string;
  isEmailVerified?: boolean; createdAtUtc?: string;
}
```

#### ApiResponse\<T\> (auth.models.ts)
```typescript
interface ApiResponse<T> {
  success: boolean; message: string;
  data: T; traceId?: string; errorCode?: string;
}
```

#### CreditCard (card.models.ts)
```typescript
interface CreditCard {
  id: string; cardholderName: string; last4: string;
  expMonth: number; expYear: number;
  issuerId: string; issuerName: string; network: string;
  isDefault: boolean; creditLimit: number;
  outstandingBalance: number; availableCredit: number;
  isVerified: boolean; isDeleted: boolean;
}
```

#### Bill (billing.service.ts)
```typescript
interface Bill {
  id: string; userId: string; cardId: string;
  cardNetwork: number; issuerId: string;
  amount: number; minDue: number; currency: string;
  billingDateUtc: string; dueDateUtc: string;
  amountPaid?: number; paidAtUtc?: string;
  status: number;  // BillStatus enum
}
enum BillStatus { Pending=1, Paid=2, Overdue=3, Cancelled=4, PartiallyPaid=5 }
```

### 10.6 Service Layer — Method Signatures

#### AuthService
```typescript
register(data: RegisterRequest): Observable<ApiResponse<any>>
verifyEmailOtp(data: VerifyOtpRequest): Observable<ApiResponse<any>>
resendVerification(data: ResendVerificationRequest): Observable<ApiResponse<any>>
forgotPassword(email: string): Observable<ApiResponse<any>>
resetPassword(data: {email, otp, newPassword}): Observable<ApiResponse<any>>
login(credentials: {email, password}): Observable<ApiResponse<any>>
getProfile(): Observable<ApiResponse<User>>
updateProfile(data: {fullName?}): Observable<ApiResponse<User>>
changePassword(data: {currentPassword, newPassword}): Observable<ApiResponse<any>>
logout(): void
```

#### DashboardService
```typescript
getCards(): Observable<ApiResponse<CreditCard[]>>
getIssuers(): Observable<ApiResponse<CardIssuer[]>>
getAllTransactions(): Observable<ApiResponse<CardTransaction[]>>
getCardById(cardId): Observable<ApiResponse<CreditCard>>
getTransactionsByCardId(cardId): Observable<ApiResponse<CardTransaction[]>>
getRewardAccount(): Observable<ApiResponse<any>>
addCard(request: CreateCardRequest): Observable<ApiResponse<CreditCard>>
deleteCard(cardId): Observable<ApiResponse<any>>
addTransaction(cardId, {type, amount, description, dateUtc?}): Observable<ApiResponse<any>>
```

#### PaymentService
```typescript
initiatePayment(request: PaymentInitiateRequest): Observable<ApiResponse<PaymentInitiateResponse>>
verifyOtp(paymentId, otpCode): Observable<ApiResponse<any>>
getMyPayments(): Observable<ApiResponse<Payment[]>>
getPaymentById(paymentId): Observable<ApiResponse<Payment>>
```

#### BillingService
```typescript
getMyBills(): Observable<ApiResponse<Bill[]>>
getBillById(billId): Observable<ApiResponse<Bill>>
getBillStatusLabel(status: number): string
getBillStatusClass(status: number): string  // returns Tailwind badge class
```

#### RewardsService
```typescript
getRewardAccount(): Observable<ApiResponse<RewardAccount>>
getRewardTiers(): Observable<ApiResponse<RewardTier[]>>
getRewardHistory(): Observable<ApiResponse<RewardTransaction[]>>
```

#### StatementService
```typescript
getMyStatements(): Observable<ApiResponse<Statement[]>>
getStatementById(statementId): Observable<ApiResponse<StatementDetail>>
getStatementByBillId(billId): Observable<ApiResponse<Statement | Statement[]>>
generateStatement(cardId): Observable<ApiResponse<any>>
```

#### AdminService
```typescript
// Identity
getAllUsers(params): Observable<ApiResponse<any>>
getUserDetails(userId): Observable<ApiResponse<User>>
getUserStats(): Observable<ApiResponse<UserStats>>
updateUserStatus(userId, status): Observable<ApiResponse<any>>
updateUserRole(userId, role): Observable<ApiResponse<any>>

// Cards
getIssuers(): Observable<ApiResponse<any[]>>
createIssuer(issuer): Observable<ApiResponse<any>>
updateIssuer(id, issuer): Observable<ApiResponse<any>>
deleteIssuer(id): Observable<ApiResponse<any>>
getCardsByUser(userId): Observable<ApiResponse<any>>
updateCardByAdmin(cardId, {creditLimit, outstandingBalance?, billingCycleStartDay?}): Observable<ApiResponse<any>>

// Billing
generateBill({userId, cardId, currency}): Observable<ApiResponse<any>>
checkOverdue(): Observable<ApiResponse<any>>
getRewardTiers(): Observable<ApiResponse<any>>
createRewardTier(tier): Observable<ApiResponse<any>>
deleteRewardTier(id): Observable<ApiResponse<any>>

// Violations
getBlockedCards(): Observable<ApiResponse<any>>
unblockCard(cardId): Observable<ApiResponse<any>>
getCardViolations(cardId): Observable<ApiResponse<any>>
clearCardViolations(cardId): Observable<ApiResponse<any>>

// Logs
getAuditLogs(params): Observable<ApiResponse<any>>
getNotificationLogs(params): Observable<ApiResponse<any>>
```

### 10.7 Route Table

| Path | Guard | Component |
|---|---|---|
| `/register` | guestGuard | RegisterComponent |
| `/verify-email` | guestGuard | VerifyEmailComponent |
| `/login` | guestGuard | LoginComponent |
| `/forgot-password` | guestGuard | ForgotPasswordComponent |
| `/reset-password` | guestGuard | ResetPasswordComponent |
| `/dashboard` | authGuard | DashboardComponent |
| `/cards/:id` | authGuard | CardDetailsComponent |
| `/profile` | authGuard | ProfileComponent |
| `/statements` | authGuard | StatementsComponent |
| `/statements/:id` | authGuard | StatementDetailComponent |
| `/bills` | authGuard | BillsComponent |
| `/rewards` | authGuard | RewardsComponent |
| `/notifications` | authGuard | NotificationsComponent |
| `/admin` | authGuard + adminGuard | AdminLayoutComponent (shell) |
| `/admin/dashboard` | (inherited) | AdminDashboardComponent |
| `/admin/users` | (inherited) | UserManagementComponent |
| `/admin/issuers` | (inherited) | IssuerManagementComponent |
| `/admin/bills` | (inherited) | BillGenerationComponent |
| `/admin/rewards` | (inherited) | RewardTiersComponent |
| `/admin/logs` | (inherited) | SystemLogsComponent |
| `/admin/violations` | (inherited) | ViolationsComponent |
| `**` | — | redirect → `/dashboard` |

---

## 11. Cross-Cutting Concerns

### 11.1 JWT Authentication

Every service (except Gateway) validates JWT independently using `AddStandardAuth()`:

```
Issuer:    "IdentityService"
Audience:  "CredVaultClients"
Algorithm: HS256 (HMAC-SHA256)
ClockSkew: 30 seconds
Expiry:    30 minutes (AccessTokenMinutes)
```

JWT Claims:
```
ClaimTypes.NameIdentifier = UserId (Guid)
ClaimTypes.Email          = user email
ClaimTypes.Role           = "user" | "admin"
```

`GetUserIdFromToken()` in `BaseApiController` reads `ClaimTypes.NameIdentifier` or falls back to `ClaimTypes.Name`.

### 11.2 Logging — Serilog

All services use Serilog with:
- Console sink (structured JSON in production)
- Rolling file sink: `logs/{service-name}-{date}.log`
- Retention: 7 days
- Custom enrichment: `Application` property = service name
- Override rules: `Microsoft.*` and `MassTransit.*` set to `Warning` to reduce noise

Log files observed in repo:
```
card-service/CardService.API/logs/card-service-20260402.log
billing-service/BillingService.API/logs/billing-service-20260403.log
payment-service/PaymentService.API/logs/payment-service-20260402.log
notification-service/NotificationService.API/logs/...
```

### 11.3 MassTransit / RabbitMQ Configuration

Registered via `AddStandardMessaging(config, configure, serviceName)`:

```csharp
x.SetKebabCaseEndpointNameFormatter();
cfg.Host(config["RabbitMQ:Host"] ?? "localhost", "/", h => {
    h.Username(config["RabbitMQ:Username"] ?? "guest");
    h.Password(config["RabbitMQ:Password"] ?? "guest");
});
cfg.ConfigureEndpoints(context,
    new KebabCaseEndpointNameFormatter(serviceName + "-", false));
```

Queue naming: `{serviceName}-{consumerName-kebab}` e.g. `billing-bill-update-saga`.

### 11.4 Database — EF Core

- Provider: SQL Server
- Connection: `Server=localhost,1434;Database=credvault_{service};User Id=sa;Password=Sql@Password!123;Encrypt=False;TrustServerCertificate=True`
- Migrations: auto-applied on startup via `dbContext.Database.MigrateAsync()`
- Global query filters: `IsDeleted == false` on `CreditCard` and `CardTransaction`
- All timestamps stored as UTC (`DATETIME2`)

### 11.5 CORS

```csharp
policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
```

Applied at gateway and all downstream services. Not restricted in development.

### 11.6 Exception Handling

`ExceptionHandlingMiddleware` is registered before all other middleware in every service's `Program.cs`. Catches any unhandled `Exception`, logs it with Serilog, and returns a structured `ApiResponse<ProblemDetails>` with HTTP 500.

### 11.7 Validation

- FluentValidation used in Payment Service (`InitiatePaymentCommandValidator`)
- `ValidationBehavior` MediatR pipeline behavior validates commands before handlers run
- Identity Service validates email format, password length in command handlers
- Card Service validates card number format (13–19 digits), expiry month (1–12), BillingCycleStartDay (1–31)

---

## 12. Database Schema — All Tables

### credvault_identity

```sql
identity_users
  Id                               UNIQUEIDENTIFIER  PK
  Email                            NVARCHAR(256)     UNIQUE NOT NULL
  FullName                         NVARCHAR(256)     NOT NULL
  PasswordHash                     NVARCHAR(MAX)     NOT NULL
  IsEmailVerified                  BIT               NOT NULL DEFAULT 0
  EmailVerificationOtp             NVARCHAR(16)      NULL
  EmailVerificationOtpExpiresAtUtc DATETIME2         NULL
  PasswordResetOtp                 NVARCHAR(16)      NULL
  PasswordResetOtpExpiresAtUtc     DATETIME2         NULL
  Status                           INT               NOT NULL  -- UserStatus enum
  Role                             INT               NOT NULL  -- UserRole enum
  CreatedAtUtc                     DATETIME2         NOT NULL
  UpdatedAtUtc                     DATETIME2         NOT NULL
```

### credvault_cards

```sql
CardIssuers
  Id            UNIQUEIDENTIFIER  PK
  Name          NVARCHAR(256)     NOT NULL
  Network       INT               NOT NULL  -- CardNetwork enum
  CreatedAtUtc  DATETIME2         NOT NULL
  UpdatedAtUtc  DATETIME2         NOT NULL
  INDEX: Network

CreditCards
  Id                   UNIQUEIDENTIFIER  PK
  UserId               UNIQUEIDENTIFIER  NOT NULL  -- ref to identity_users
  IssuerId             UNIQUEIDENTIFIER  NOT NULL  FK → CardIssuers
  CardholderName       NVARCHAR(256)     NOT NULL
  ExpMonth             INT               NOT NULL
  ExpYear              INT               NOT NULL
  Last4                NVARCHAR(4)       NOT NULL
  MaskedNumber         NVARCHAR(32)      NOT NULL
  CreditLimit          DECIMAL(18,2)     NOT NULL DEFAULT 0
  OutstandingBalance   DECIMAL(18,2)     NOT NULL DEFAULT 0
  BillingCycleStartDay INT               NOT NULL DEFAULT 1
  StrikeCount          INT               NOT NULL DEFAULT 0
  IsBlocked            BIT               NOT NULL DEFAULT 0
  BlockedAtUtc         DATETIME2         NULL
  UnblockedAtUtc       DATETIME2         NULL
  IsDefault            BIT               NOT NULL DEFAULT 0
  IsVerified           BIT               NOT NULL DEFAULT 0
  VerifiedAtUtc        DATETIME2         NULL
  IsDeleted            BIT               NOT NULL DEFAULT 0
  DeletedAtUtc         DATETIME2         NULL
  CreatedAtUtc         DATETIME2         NOT NULL
  UpdatedAtUtc         DATETIME2         NOT NULL
  GLOBAL QUERY FILTER: IsDeleted = 0
  INDEX: UserId, (UserId, IsDefault), IssuerId

CardTransactions
  Id           UNIQUEIDENTIFIER  PK
  CardId       UNIQUEIDENTIFIER  NOT NULL  FK → CreditCards
  UserId       UNIQUEIDENTIFIER  NOT NULL
  Type         INT               NOT NULL  -- TransactionType: Purchase=1, Payment=2, Refund=3
  Amount       DECIMAL(18,2)     NOT NULL
  Description  NVARCHAR(512)     NOT NULL
  DateUtc      DATETIME2         NOT NULL
  GLOBAL QUERY FILTER: Card.IsDeleted = 0
  INDEX: CardId, UserId, DateUtc

CardViolations
  Id            UNIQUEIDENTIFIER  PK
  CardId        UNIQUEIDENTIFIER  NOT NULL  FK → CreditCards
  UserId        UNIQUEIDENTIFIER  NOT NULL
  BillId        UNIQUEIDENTIFIER  NULL
  Type          INT               NOT NULL  -- ViolationType: LatePayment=1, OverdueBill=2, MissedPayment=3
  StrikeCount   INT               NOT NULL
  Reason        NVARCHAR(512)     NOT NULL
  PenaltyAmount DECIMAL(18,2)     NOT NULL DEFAULT 0
  IsActive      BIT               NOT NULL DEFAULT 1
  AppliedAtUtc  DATETIME2         NOT NULL
  ClearedAtUtc  DATETIME2         NULL
  INDEX: CardId, UserId, (CardId, IsActive)
```

### credvault_billing

```sql
Bills
  Id             UNIQUEIDENTIFIER  PK
  UserId         UNIQUEIDENTIFIER  NOT NULL
  CardId         UNIQUEIDENTIFIER  NOT NULL  -- cross-service ref, no FK
  CardNetwork    INT               NOT NULL
  IssuerId       UNIQUEIDENTIFIER  NOT NULL
  Amount         DECIMAL(18,2)     NOT NULL
  MinDue         DECIMAL(18,2)     NOT NULL
  Currency       NVARCHAR(3)       NOT NULL DEFAULT 'USD'
  BillingDateUtc DATETIME2         NOT NULL
  DueDateUtc     DATETIME2         NOT NULL
  AmountPaid     DECIMAL(18,2)     NULL
  PaidAtUtc      DATETIME2         NULL
  Status         INT               NOT NULL  -- BillStatus: Pending=1, Paid=2, Overdue=3, Cancelled=4, PartiallyPaid=5
  CreatedAtUtc   DATETIME2         NOT NULL
  UpdatedAtUtc   DATETIME2         NOT NULL

RewardTiers
  Id               UNIQUEIDENTIFIER  PK
  CardNetwork      INT               NOT NULL
  IssuerId         UNIQUEIDENTIFIER  NULL  -- NULL = network-wide
  MinSpend         DECIMAL(18,2)     NOT NULL
  RewardRate       DECIMAL(5,4)      NOT NULL  -- e.g. 0.0150 = 1.5%
  EffectiveFromUtc DATETIME2         NOT NULL
  EffectiveToUtc   DATETIME2         NULL
  CreatedAtUtc     DATETIME2         NOT NULL
  UpdatedAtUtc     DATETIME2         NOT NULL

RewardAccounts
  Id            UNIQUEIDENTIFIER  PK
  UserId        UNIQUEIDENTIFIER  NOT NULL  UNIQUE
  RewardTierId  UNIQUEIDENTIFIER  NOT NULL  FK → RewardTiers
  PointsBalance DECIMAL(18,2)     NOT NULL DEFAULT 0
  CreatedAtUtc  DATETIME2         NOT NULL
  UpdatedAtUtc  DATETIME2         NOT NULL

RewardTransactions
  Id                UNIQUEIDENTIFIER  PK
  RewardAccountId   UNIQUEIDENTIFIER  NOT NULL  FK → RewardAccounts
  BillId            UNIQUEIDENTIFIER  NOT NULL  FK → Bills
  Points            DECIMAL(18,2)     NOT NULL
  Type              INT               NOT NULL  -- Earned=1, Adjusted=2, Redeemed=3, Reversed=4
  ReversedAtUtc     DATETIME2         NULL
  CreatedAtUtc      DATETIME2         NOT NULL

Statements
  Id               UNIQUEIDENTIFIER  PK
  UserId           UNIQUEIDENTIFIER  NOT NULL
  CardId           UNIQUEIDENTIFIER  NOT NULL
  BillId           UNIQUEIDENTIFIER  NULL
  StatementPeriod  NVARCHAR(100)     NOT NULL  -- e.g. "Mar 2026"
  PeriodStartUtc   DATETIME2         NOT NULL
  PeriodEndUtc     DATETIME2         NOT NULL
  GeneratedAtUtc   DATETIME2         NOT NULL
  DueDateUtc       DATETIME2         NULL
  OpeningBalance   DECIMAL(18,2)     NOT NULL
  TotalPurchases   DECIMAL(18,2)     NOT NULL
  TotalPayments    DECIMAL(18,2)     NOT NULL
  TotalRefunds     DECIMAL(18,2)     NOT NULL
  PenaltyCharges   DECIMAL(18,2)     NOT NULL
  InterestCharges  DECIMAL(18,2)     NOT NULL
  ClosingBalance   DECIMAL(18,2)     NOT NULL
  MinimumDue       DECIMAL(18,2)     NOT NULL
  AmountPaid       DECIMAL(18,2)     NOT NULL DEFAULT 0
  PaidAtUtc        DATETIME2         NULL
  Status           INT               NOT NULL  -- Generated=1, Paid=2, Overdue=3, PartiallyPaid=4
  CardLast4        NVARCHAR(4)       NOT NULL
  CardNetwork      NVARCHAR(32)      NOT NULL
  IssuerName       NVARCHAR(256)     NOT NULL
  CreditLimit      DECIMAL(18,2)     NOT NULL
  AvailableCredit  DECIMAL(18,2)     NOT NULL
  Notes            NVARCHAR(MAX)     NULL
  CreatedAtUtc     DATETIME2         NOT NULL
  UpdatedAtUtc     DATETIME2         NOT NULL

StatementTransactions
  Id                  UNIQUEIDENTIFIER  PK
  StatementId         UNIQUEIDENTIFIER  NOT NULL  FK → Statements
  SourceTransactionId UNIQUEIDENTIFIER  NULL
  Type                NVARCHAR(50)      NOT NULL
  Amount              DECIMAL(18,2)     NOT NULL
  Description         NVARCHAR(256)     NOT NULL
  DateUtc             DATETIME2         NOT NULL
  CreatedAtUtc        DATETIME2         NOT NULL
```

### credvault_payments

```sql
Payments
  Id              UNIQUEIDENTIFIER  PK
  UserId          UNIQUEIDENTIFIER  NOT NULL
  CardId          UNIQUEIDENTIFIER  NOT NULL
  BillId          UNIQUEIDENTIFIER  NOT NULL
  Amount          DECIMAL(18,2)     NOT NULL
  PaymentType     NVARCHAR(32)      NOT NULL  -- Full, Partial, Scheduled
  Status          NVARCHAR(32)      NOT NULL  -- Initiated, Processing, Completed, Failed, Reversed, Cancelled
  FailureReason   NVARCHAR(500)     NULL
  OtpCode         NVARCHAR(16)      NULL
  OtpExpiresAtUtc DATETIME2         NULL
  IsDeleted       BIT               NOT NULL DEFAULT 0
  DeletedAtUtc    DATETIME2         NULL
  CreatedAtUtc    DATETIME2         NOT NULL
  UpdatedAtUtc    DATETIME2         NOT NULL
  INDEX: UserId, BillId

Transactions
  Id           UNIQUEIDENTIFIER  PK
  PaymentId    UNIQUEIDENTIFIER  NOT NULL  FK → Payments
  UserId       UNIQUEIDENTIFIER  NOT NULL
  Amount       DECIMAL(18,2)     NOT NULL
  Type         INT               NOT NULL  -- Payment=0, Reversal=1
  Description  NVARCHAR(250)     NOT NULL
  CreatedAtUtc DATETIME2         NOT NULL
  INDEX: UserId, PaymentId

PaymentOrchestrationSagas
  CorrelationId        UNIQUEIDENTIFIER  PK  -- = PaymentId
  CurrentState         NVARCHAR(64)      NOT NULL
  PaymentId            UNIQUEIDENTIFIER  NOT NULL
  UserId               UNIQUEIDENTIFIER  NOT NULL
  Email                NVARCHAR(256)     NULL
  FullName             NVARCHAR(256)     NULL
  CardId               UNIQUEIDENTIFIER  NOT NULL
  BillId               UNIQUEIDENTIFIER  NOT NULL
  Amount               DECIMAL(18,2)     NOT NULL
  PaymentType          NVARCHAR(32)      NULL
  OtpCode              NVARCHAR(16)      NULL
  OtpExpiresAtUtc      DATETIME2         NULL
  OtpVerified          BIT               NOT NULL DEFAULT 0
  PaymentProcessed     BIT               NOT NULL DEFAULT 0
  BillUpdated          BIT               NOT NULL DEFAULT 0
  CardDeducted         BIT               NOT NULL DEFAULT 0
  PaymentError         NVARCHAR(512)     NULL
  BillUpdateError      NVARCHAR(512)     NULL
  CardDeductionError   NVARCHAR(512)     NULL
  CompensationReason   NVARCHAR(1024)    NULL
  CompensationAttempts INT               NOT NULL DEFAULT 0
  CreatedAtUtc         DATETIME2         NOT NULL
  UpdatedAtUtc         DATETIME2         NOT NULL
```

### credvault_notifications

```sql
NotificationLogs
  Id           UNIQUEIDENTIFIER  PK
  UserId       UNIQUEIDENTIFIER  NULL
  Recipient    NVARCHAR(256)     NOT NULL
  Type         NVARCHAR(32)      NOT NULL  -- Email, Sms, Push
  Subject      NVARCHAR(512)     NOT NULL
  Body         NVARCHAR(MAX)     NOT NULL
  IsSuccess    BIT               NOT NULL
  ErrorMessage NVARCHAR(MAX)     NULL
  TraceId      NVARCHAR(128)     NULL
  CreatedAtUtc DATETIME2         NOT NULL

AuditLogs
  Id          UNIQUEIDENTIFIER  PK
  EntityName  NVARCHAR(256)     NOT NULL
  EntityId    NVARCHAR(256)     NOT NULL
  Action      NVARCHAR(64)      NOT NULL  -- Consumed, Processed, Failed
  UserId      NVARCHAR(256)     NULL
  Changes     NVARCHAR(MAX)     NULL  -- JSON
  TraceId     NVARCHAR(128)     NULL
  CreatedAtUtc DATETIME2        NOT NULL
```

---

## 13. Complete Event Catalog

### Domain Events (published to `notification-domain-event`)

#### IUserRegistered
```csharp
Guid     UserId
string   Email
string   FullName
DateTime CreatedAtUtc
```

#### IUserOtpGenerated
```csharp
Guid     UserId
string   Email
string   FullName
string   OtpCode      // 6-digit
string   Purpose      // "EmailVerification" | "PasswordReset"
DateTime ExpiresAtUtc
```

#### IUserDeleted
```csharp
Guid     UserId
DateTime DeletedAtUtc
```

#### ICardAdded
```csharp
Guid     CardId
Guid     UserId
string   Email
string   FullName
string   CardNumberLast4
string   CardHolderName
DateTime AddedAt
```

#### IBillGenerated
```csharp
Guid     BillId
Guid     UserId
string   Email
string   FullName
Guid     CardId
decimal  Amount
DateTime DueDate
DateTime GeneratedAt
```

#### IPaymentOtpGenerated
```csharp
Guid     PaymentId
Guid     UserId
string   Email
string   FullName
decimal  Amount
string   OtpCode
DateTime ExpiresAtUtc
```

#### IPaymentCompleted
```csharp
Guid     PaymentId
Guid     UserId
string   Email
string   FullName
Guid     CardId
Guid     BillId
decimal  Amount
DateTime CompletedAt
```

#### IPaymentFailed
```csharp
Guid     PaymentId
Guid     UserId
string   Email
string   FullName
decimal  Amount
string   Reason
DateTime FailedAt
```

#### IPaymentReversed
```csharp
Guid     PaymentId
Guid     UserId
Guid     BillId
Guid     CardId
decimal  Amount
decimal  PointsDeducted
DateTime ReversedAt
```

---

### SAGA Orchestration Events (internal to payment flow)

#### IStartPaymentOrchestration
```csharp
Guid     CorrelationId  // = PaymentId
Guid     PaymentId
Guid     UserId
string   Email
string   FullName
Guid     CardId
Guid     BillId
decimal  Amount
string   PaymentType
string   OtpCode
DateTime StartedAt
```

#### IBillOverdueDetected
```csharp
Guid     BillId
Guid     CardId
Guid     UserId
decimal  OverdueAmount
DateTime DueDate
int      DaysOverdue
DateTime DetectedAt
```

#### IOtpVerified / IOtpFailed
```csharp
// IOtpVerified
Guid     CorrelationId
Guid     PaymentId
string   OtpCode
DateTime VerifiedAt

// IOtpFailed
Guid     CorrelationId
Guid     PaymentId
string   Reason
DateTime FailedAt
```

#### IPaymentProcessRequested / Succeeded / Failed
```csharp
// Requested
Guid     CorrelationId
Guid     PaymentId
Guid     UserId
decimal  Amount
DateTime RequestedAt

// Succeeded
Guid     CorrelationId
Guid     PaymentId
DateTime SucceededAt

// Failed
Guid     CorrelationId
Guid     PaymentId
string   Reason
DateTime FailedAt
```

#### IBillUpdateRequested / Succeeded / Failed
```csharp
// Requested
Guid     CorrelationId
Guid     PaymentId
Guid     UserId
Guid     BillId
Guid     CardId
decimal  Amount
DateTime RequestedAt

// Succeeded
Guid     CorrelationId
Guid     BillId
Guid     CardId
DateTime SucceededAt

// Failed
Guid     CorrelationId
Guid     BillId
string   Reason
DateTime FailedAt
```

#### ICardDeductionRequested / Succeeded / Failed
```csharp
// Requested
Guid     CorrelationId
Guid     PaymentId
Guid     UserId
Guid     CardId
decimal  Amount
DateTime RequestedAt

// Succeeded
Guid     CorrelationId
Guid     CardId
decimal  NewBalance
DateTime SucceededAt

// Failed
Guid     CorrelationId
Guid     CardId
string   Reason
DateTime FailedAt
```

#### IRevertBillUpdateRequested / Succeeded / Failed
```csharp
// Requested
Guid     CorrelationId
Guid     PaymentId
Guid     UserId
Guid     BillId
decimal  Amount
DateTime RequestedAt

// Succeeded / Failed (same shape as BillUpdate)
```

#### IRevertPaymentRequested / Succeeded / Failed
```csharp
// Requested
Guid     CorrelationId
Guid     PaymentId
Guid     UserId
Guid     BillId
Guid     CardId
decimal  Amount
DateTime RequestedAt

// Succeeded
Guid     CorrelationId
Guid     PaymentId
DateTime SucceededAt

// Failed
Guid     CorrelationId
Guid     PaymentId
string   Reason
DateTime FailedAt
```

---

## 14. Request/Response Contracts

### Identity Service

```
POST /auth/register
  Body:    { email: string, password: string, fullName: string }
  201:     ApiResponse<null>

POST /auth/login
  Body:    { email: string, password: string }
  200:     ApiResponse<{ accessToken: string, user: User }>
  401:     ErrorCode="InvalidCredentials"
  403:     ErrorCode="AccountLocked"

POST /auth/verify-email-otp
  Body:    { email: string, otp: string }
  200:     ApiResponse<{ accessToken: string, user: User }>

POST /auth/forgot-password
  Body:    { email: string }
  200:     ApiResponse<null>

POST /auth/reset-password
  Body:    { email: string, otp: string, newPassword: string }
  200:     ApiResponse<null>

PUT /users/me
  Body:    { fullName: string }
  200:     ApiResponse<User>

PUT /users/me/password
  Body:    { currentPassword: string, newPassword: string }
  200:     ApiResponse<null>

PUT /users/{userId}/status  [admin]
  Body:    { Status: "active" | "suspended" | "pending-verification" }
  200:     ApiResponse<null>
```

### Card Service

```
POST /cards
  Body:    { cardholderName, expMonth, expYear, cardNumber, issuerId, isDefault }
  201:     ApiResponse<CreditCard>
  409:     ErrorCode="Conflict" (duplicate card)

GET /cards
  200:     ApiResponse<CreditCard[]>

GET /cards/{cardId}/health
  200:     ApiResponse<{ healthScore: number, grade: string, utilization: number }>

PUT /cards/{cardId}/admin  [admin]
  Body:    { CreditLimit, OutstandingBalance?, BillingCycleStartDay? }
  200:     ApiResponse<null>

POST /cards/{cardId}/transactions
  Body:    { type: 1|2|3, amount: number, description: string, dateUtc?: string }
  201:     ApiResponse<CardTransaction>
```

### Payment Service

```
POST /payments/initiate
  Body:    { cardId, billId, amount, paymentType: "Full"|"Partial"|"Scheduled" }
  201:     ApiResponse<{ paymentId, otpRequired: true, status: "Pending OTP Verification" }>

POST /payments/{paymentId}/verify-otp
  Body:    { otpCode: string }
  200:     ApiResponse<{ paymentId, status: "Processing" }>
  400:     OTP invalid / expired / wrong status
```

### Billing Service

```
POST /billing/bills/admin/generate-bill  [admin]
  Body:    { UserId, CardId, Currency }
  201:     ApiResponse<Bill>

GET /billing/rewards/account
  200:     ApiResponse<{ id, userId, rewardTierId, pointsBalance }>

POST /billing/rewards/tiers  [admin]
  Body:    { CardNetwork, IssuerId?, MinSpend, RewardRate, EffectiveFromUtc, EffectiveToUtc? }
  201:     ApiResponse<RewardTier>

GET /billing/statements/{statementId}
  200:     ApiResponse<StatementDetail>  // includes transactions[]
```

### Notification Service

```
GET /notifications/logs?email=...&page=1&pageSize=10
  200:     { total, page, pageSize, logs: NotificationLog[] }

GET /notifications/audit?traceId=...&page=1&pageSize=10
  200:     { total, page, pageSize, logs: AuditLog[] }
```
