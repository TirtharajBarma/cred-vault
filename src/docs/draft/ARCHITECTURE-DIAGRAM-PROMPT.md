# CredVault - Complete System Specification

## MASTER DOCUMENT FOR AI CODE GENERATION

> **Purpose:** This document contains EVERYTHING required to generate complete architectural diagrams, HLD, LLD, sequence diagrams, ER diagrams, use-case documents, API references, and all other system documentation for the CredVault Credit Card Management Platform.
>
> Feed this entire document to any AI (Claude, GPT, etc.) and it should be able to produce all documentation artifacts.

---

## TABLE OF CONTENTS

1. [Project Overview](#1-project-overview)
2. [System Goals & Non-Functional Requirements](#2-system-goals--non-functional-requirements)
3. [High-Level Architecture (HLD)](#3-high-level-architecture-hld)
4. [Low-Level Design (LLD) - Per Service](#4-low-level-design-lld---per-service)
5. [Complete API Reference](#5-complete-api-reference)
6. [Entity Relationship (ER) Diagrams](#6-entity-relationship-er-diagrams)
7. [Sequence Diagrams](#7-sequence-diagrams)
8. [Use Case Specifications](#8-use-case-specifications)
9. [State Machines](#9-state-machines)
10. [Event Schema Reference](#10-event-schema-reference)
11. [Enum Reference](#11-enum-reference)
12. [DTO Specifications](#12-dto-specifications)
13. [Authentication & Authorization](#13-authentication--authorization)
14. [Saga Orchestration Deep Dive](#14-saga-orchestration-deep-dive)
15. [Database Schema (Complete)](#15-database-schema-complete)
16. [External Integrations](#16-external-integrations)
17. [Error Handling & Response Format](#17-error-handling--response-format)
18. [Logging & Observability](#18-logging--observability)
19. [Deployment Architecture](#19-deployment-architecture)
20. [Testing Strategy](#20-testing-strategy)
21. [Business Rules](#21-business-rules)
22. [Frontend Architecture](#22-frontend-architecture)
23. [Shared Contracts Library](#23-shared-contracts-library)
24. [Diagram Generation Instructions](#24-diagram-generation-instructions)

---

## 1. PROJECT OVERVIEW

| Attribute | Value |
|-----------|-------|
| **Project Name** | CredVault |
| **Type** | Enterprise Credit Card Management Platform |
| **Architecture Style** | Microservices with Event-Driven Communication + Saga Orchestration |
| **Primary Domain** | Financial Services / Credit Card Management |
| **Self-Learning Project** | Demonstrates microservices patterns: CQRS, Saga, Event Sourcing, API Gateway |

### Core Capabilities
- User registration with email OTP verification
- Google OAuth SSO login
- Credit card lifecycle management (add, view, manage cards)
- Automated bill generation and statement creation
- Rewards point system with tier-based earning
- Wallet-based and card-direct payments with OTP 2FA
- Distributed transaction management via Saga pattern
- Email notifications for all critical events
- Admin panel for user, card, billing, and reward management

---

## 2. SYSTEM GOALS & NON-FUNCTIONAL REQUIREMENTS

### Functional Goals
1. Allow users to register, verify email, and manage their profile
2. Enable users to add and manage multiple credit cards
3. Generate bills and statements automatically
4. Process payments securely with OTP-based two-factor authentication
5. Manage rewards points with tier-based earning rates
6. Provide wallet functionality for payments and Razorpay top-ups
7. Send email notifications for all critical system events
8. Provide admin capabilities for platform management

### Non-Functional Requirements
| Requirement | Specification |
|-------------|---------------|
| **Scalability** | Each service scales independently; stateless services behind gateway |
| **Availability** | Services are autonomous; one service failure doesn't cascade |
| **Consistency** | Eventual consistency via events and sagas; no distributed transactions |
| **Security** | JWT auth, RBAC, encrypted card numbers, OTP 2FA for payments |
| **Performance** | Async communication for non-critical paths; CQRS for read-heavy ops |
| **Reliability** | Retry policies with exponential backoff; outbox pattern for message delivery |
| **Maintainability** | Clean Architecture per service; shared contracts; consistent patterns |
| **Observability** | Structured logging with Serilog; correlation IDs; audit trails |
| **Portability** | Docker Compose for local; containerized services |

---

## 3. HIGH-LEVEL ARCHITECTURE (HLD)

### 3.1 System Components

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              CREDVAULT PLATFORM                                 │
│                                                                                 │
│   ┌───────────────────────────────────────────────────────────────────────────┐ │
│   │                         CLIENT TIER                                       │ │
│   │                                                                           │ │
│   │  ┌─────────────────────┐                                                 │ │
│   │  │  Angular Frontend   │ ← sessionStorage (JWT), RxJS, Tailwind CSS      │ │
│   │  │  (Port: 4200)       │ ← Guards: auth, guest, admin                    │ │
│   │  │  SSR via Express    │ ← Interceptor: auto Bearer token, 401 handling   │ │
│   │  └─────────┬───────────┘                                                 │ │
│   └────────────┼─────────────────────────────────────────────────────────────┘ │
│                │ HTTPS (All API calls)                                          │
│   ┌────────────▼─────────────────────────────────────────────────────────────┐ │
│   │                         GATEWAY TIER                                      │ │
│   │                                                                           │ │
│   │  ┌─────────────────────┐                                                 │ │
│   │  │  Ocelot API Gateway │ ← Route-by-prefix, Rate limiting ready          │ │
│   │  │  (Port: 5006)       │ ← Two configs: localhost + Docker               │ │
│   │  └─────────┬───────────┘                                                 │ │
│   └────────────┼─────────────────────────────────────────────────────────────┘ │
│                │                                                                │
│   ┌────────────▼─────────────────────────────────────────────────────────────┐ │
│   │                       SERVICE TIER (Microservices)                        │ │
│   │                                                                           │ │
│   │  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐    │ │
│   │  │  Identity    │ │    Card      │ │   Billing    │ │   Payment    │    │ │
│   │  │  Service     │ │   Service    │ │   Service    │ │   Service    │    │ │
│   │  │  :5001       │ │   :5002      │ │   :5003      │ │   :5004      │    │ │
│   │  └──────┬───────┘ └──────┬───────┘ └──────┬───────┘ └──────┬───────┘    │ │
│   │         │                │                │                │             │ │
│   │  ┌──────▼───────┐ ┌──────▼───────┐ ┌──────▼───────┐ ┌──────▼───────┐    │ │
│   │  │ credvault_   │ │ credvault_   │ │ credvault_   │ │ credvault_   │    │ │
│   │  │ identity     │ │ cards        │ │ billing      │ │ payments     │    │ │
│   │  │              │ │              │ │              │ │              │    │ │
│   │  │ 1 table      │ │ 3 tables     │ │ 6 tables     │ │ 6 tables     │    │ │
│   │  └──────────────┘ └──────────────┘ └──────────────┘ └──────────────┘    │ │
│   │                                                                           │ │
│   │  ┌──────────────┐                                                        │ │
│   │  │ Notification │                                                        │ │
│   │  │  Service     │                                                        │ │
│   │  │  :5005       │                                                        │ │
│   │  └──────┬───────┘                                                        │ │
│   │         │                                                                 │ │
│   │  ┌──────▼───────┐                                                        │ │
│   │  │ credvault_   │                                                        │ │
│   │  │ notifications│                                                        │ │
│   │  │              │                                                        │ │
│   │  │ 2 tables     │                                                        │ │
│   │  └──────────────┘                                                        │ │
│   └────────────┼─────────────────────────────────────────────────────────────┘ │
│                │                                                                │
│   ┌────────────▼─────────────────────────────────────────────────────────────┐ │
│   │                      MESSAGING TIER                                       │ │
│   │                                                                           │ │
│   │  ┌─────────────────────┐                                                 │ │
│   │  │     RabbitMQ        │ ← MassTransit, InMemoryOutbox                   │ │
│   │  │  :5672 / :15672     │ ← Retry: 1s → 5s → 15s exponential backoff     │ │
│   │  └─────────────────────┘                                                 │ │
│   └───────────────────────────────────────────────────────────────────────────┘ │
│                                                                                 │
│   ┌───────────────────────────────────────────────────────────────────────────┐ │
│   │                      EXTERNAL SERVICES                                    │ │
│   │                                                                           │ │
│   │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                    │ │
│   │  │  Google OAuth│  │   Razorpay   │  │  Gmail SMTP  │                    │ │
│   │  └──────────────┘  └──────────────┘  └──────────────┘                    │ │
│   └───────────────────────────────────────────────────────────────────────────┘ │
│                                                                                 │
│   ┌───────────────────────────────────────────────────────────────────────────┐ │
│   │                      INFRASTRUCTURE                                       │ │
│   │                                                                           │ │
│   │  ┌─────────────────────┐                                                 │ │
│   │  │   SQL Server 2022   │ ← 5 isolated databases                          │ │
│   │  │      :1433          │ ← EF Core Code-First migrations                 │ │
│   │  └─────────────────────┘                                                 │ │
│   └───────────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────────┘
```

### 3.2 Communication Patterns

| Pattern | Type | Protocol | Usage |
|---------|------|----------|-------|
| Client → Gateway | Sync | HTTPS | All user requests |
| Gateway → Service | Sync | HTTP (internal) | Request routing |
| Service → Service (async) | Async | RabbitMQ/AMQP | Events, notifications, saga |
| Service → DB | Sync | TCP/SQL | Data persistence |
| Service → External | Sync | HTTPS/SMTP | OAuth, payments, email |

### 3.3 Service Responsibilities Matrix

| Feature | Identity | Card | Billing | Payment | Notification |
|---------|----------|------|---------|---------|--------------|
| User Registration | Primary | - | - | - | Email |
| Authentication | Primary | Validates JWT | Validates JWT | Validates JWT | - |
| User Management | Primary | - | - | - | Audit |
| Card CRUD | - | Primary | - | Deduction (saga) | Email |
| Bill Generation | - | - | Primary | - | - |
| Statement Creation | - | - | Primary | - | - |
| Rewards System | - | - | Primary | Redeem (saga) | - |
| Payment Initiation | - | - | - | Primary | OTP Email |
| Wallet Management | - | - | - | Primary | - |
| Email Notifications | Publishes event | Publishes event | - | Publishes event | Primary |
| Audit Logging | - | - | - | - | Primary |

---

## 4. LOW-LEVEL DESIGN (LLD) - PER SERVICE

### 4.1 Clean Architecture Layers (Applied to All Services)

```
┌─────────────────────────────────────────────────────────────┐
│                         API Layer                            │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ Controllers, Program.cs, Middleware, Dockerfile      │    │
│  │ - HTTP request handling                             │    │
│  │ - Model binding                                     │    │
│  │ - Response formatting (via BaseApiController)       │    │
│  └─────────────────────────────────────────────────────┘    │
├─────────────────────────────────────────────────────────────┤
│                     Application Layer                        │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ Commands, Queries, DTOs, Validators, Handlers       │    │
│  │ - Business logic (CQRS via MediatR)                 │    │
│  │ - FluentValidation validators                       │    │
│  │ - Abstractions (interfaces)                         │    │
│  │ - Saga state machines (Payment Service)             │    │
│  └─────────────────────────────────────────────────────┘    │
├─────────────────────────────────────────────────────────────┤
│                    Infrastructure Layer                      │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ EF Core DbContext, Repositories, Migrations         │    │
│  │ - Data persistence                                  │    │
│  │ - Messaging (MassTransit consumers/publishers)      │    │
│  │ - Background jobs                                   │    │
│  │ - External service clients                          │    │
│  └─────────────────────────────────────────────────────┘    │
├─────────────────────────────────────────────────────────────┤
│                       Domain Layer                           │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ Entities, Enums, Domain Events, Value Objects       │    │
│  │ - Core business models                              │    │
│  │ - No external dependencies                          │    │
│  │ - Pure business rules                               │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

### 4.2 Identity Service (:5001)

#### Purpose
Manages user identity, authentication, and authorization.

#### Domain Entities
- **IdentityUser**: Id, Email, FullName, PasswordHash (nullable for SSO), IsEmailVerified, EmailVerificationOtp, PasswordResetOtp, Status, Role, CreatedAtUtc, UpdatedAtUtc

#### Application Commands
- `RegisterUserCommand` - Email, FullName, Password → creates PendingVerification user
- `LoginUserCommand` - Email, Password → validates, returns JWT
- `GoogleLoginCommand` - Google IdToken → validates, creates/logs in user, returns JWT
- `VerifyEmailOtpCommand` - Email, OTP → verifies, marks email verified, returns JWT
- `ResendVerificationCommand` - Email → regenerates OTP, publishes event
- `ForgotPasswordCommand` - Email → generates reset OTP, publishes event
- `ResetPasswordCommand` - Email, OTP, NewPassword → validates, updates password
- `GetUserProfileQuery` - UserId → returns profile
- `UpdateUserProfileCommand` - UserId, FullName → updates profile
- `ChangePasswordCommand` - UserId, OldPassword, NewPassword → validates old, updates
- `AdminGetUserQuery` - UserId → returns user details (admin)
- `AdminListUsersQuery` - Page, PageSize → paginated user list (admin)
- `AdminUpdateUserStatusCommand` - UserId, Status → updates status (admin)
- `AdminUpdateUserRoleCommand` - UserId, Role → updates role (admin)
- `AdminGetUserStatsQuery` → returns aggregated stats (admin)

#### Application Queries
- `GetUserProfileQuery`
- `AdminListUsersQuery`
- `AdminGetUserStatsQuery`

#### Events Published
- `IUserRegistered` - When new user registers
- `IUserOtpGenerated` - When OTP generated (email verification or password reset)

#### Infrastructure
- `IdentityDbContext` - EF Core DbContext for credvault_identity
- `UserRepository` - CRUD operations for IdentityUser
- Auto-migration on startup

---

### 4.3 Card Service (:5002)

#### Purpose
Manages credit card lifecycle, transactions, and card issuers.

#### Domain Entities
- **CreditCard**: Id, UserId, IssuerId, CardholderName, Last4, MaskedNumber, EncryptedCardNumber, ExpMonth, ExpYear, CreditLimit, OutstandingBalance, BillingCycleStartDay, IsDefault, IsVerified, IsDeleted, CreatedAtUtc, UpdatedAtUtc
- **CardTransaction**: Id, CardId, UserId, Type, Amount, Description, DateUtc
- **CardIssuer**: Id, Name, Network, CreatedAtUtc, UpdatedAtUtc

#### Application Commands
- `AddCardCommand` - UserId, CardDetails → creates card (encrypted), publishes ICardAdded
- `UpdateCardCommand` - CardId, Updates → updates card
- `DeleteCardCommand` - CardId → soft-delete (IsDeleted=true)
- `AddCardTransactionCommand` - CardId, Type, Amount → records transaction
- `AdminUpdateCardCommand` - CardId, Updates → admin card update
- `AddIssuerCommand` - Name, Network → creates issuer
- `UpdateIssuerCommand` - IssuerId, Updates → updates issuer
- `DeleteIssuerCommand` - IssuerId → deletes issuer

#### Application Queries
- `GetCardsByUserQuery` - UserId → returns user's cards
- `GetCardByIdQuery` - CardId → returns card details
- `GetCardTransactionsQuery` - CardId → returns transactions
- `GetAllUserTransactionsQuery` - UserId → all transactions across cards
- `GetIssuerByIdQuery` - IssuerId → returns issuer
- `ListIssuersQuery` → returns all issuers
- `AdminGetCardQuery` - CardId → admin card details
- `AdminListCardsQuery` - Page, PageSize → paginated cards

#### Events Published
- `ICardAdded` - When new card is added

#### Infrastructure
- `CardDbContext` - EF Core DbContext for credvault_cards
- Encryption service for card numbers
- Soft-delete filter in queries

---

### 4.4 Billing Service (:5003)

#### Purpose
Manages bills, statements, rewards accounts, and reward tiers.

#### Domain Entities
- **Bill**: Id, UserId, CardId, CardNetwork, IssuerId, Amount, MinDue, Currency, BillingDateUtc, DueDateUtc, AmountPaid, PaidAtUtc, Status, CreatedAtUtc, UpdatedAtUtc
- **Statement**: Id, UserId, CardId, BillId, StatementPeriod, PeriodStartUtc, PeriodEndUtc, OpeningBalance, TotalPurchases, TotalPayments, TotalRefunds, PenaltyCharges, InterestCharges, ClosingBalance, MinimumDue, AmountPaid, Status, CardLast4, CardNetwork, IssuerName, CreditLimit, AvailableCredit, CreatedAtUtc, UpdatedAtUtc
- **StatementTransaction**: Id, StatementId, SourceTransactionId, Type, Amount, Description, DateUtc
- **RewardAccount**: Id, UserId, RewardTierId, PointsBalance, CreatedAtUtc, UpdatedAtUtc
- **RewardTier**: Id, CardNetwork, IssuerId (nullable), MinSpend, RewardRate, EffectiveFromUtc, EffectiveToUtc, CreatedAtUtc, UpdatedAtUtc
- **RewardTransaction**: Id, RewardAccountId, BillId, Points, Type, CreatedAtUtc

#### Application Commands
- `GenerateBillCommand` - UserId, CardId → creates bill (admin)
- `CheckOverdueBillsCommand` → marks overdue bills (admin)
- `PayBillCommand` - BillId, Amount → updates bill status
- `CreateStatementCommand` - CardId, Period → creates statement with transactions
- `CreateRewardAccountCommand` - UserId → initializes reward account
- `EarnRewardsCommand` - RewardAccountId, BillId, Points → adds earned points
- `RedeemRewardsCommand` - RewardAccountId, Points → deducts points (saga step)
- `ReverseRewardsCommand` - RewardAccountId, Points → reverses points (compensation)
- `CreateRewardTierCommand` - Network, IssuerId, MinSpend, Rate → creates tier
- `UpdateRewardTierCommand` - TierId, Updates → updates tier
- `DeleteRewardTierCommand` - TierId → deletes tier

#### Application Queries
- `GetBillsByUserQuery` - UserId → returns user's bills
- `GetBillByIdQuery` - BillId → returns bill
- `HasPendingBillQuery` - CardId → checks for pending bills
- `GetStatementsByUserQuery` - UserId → returns statements
- `GetStatementByIdQuery` - StatementId → returns statement with details
- `GetStatementTransactionsQuery` - StatementId → returns statement transactions
- `GetRewardAccountQuery` - UserId → returns reward account with balance
- `GetRewardTransactionsQuery` - UserId → returns reward history
- `ListRewardTiersQuery` → returns all tiers

#### Events Consumed (Saga)
- Reward redemption requests from Payment Service
- Reward reversal requests from Payment Service (compensation)

#### Infrastructure
- `BillingDbContext` - EF Core DbContext for credvault_billing
- Auto bill generation logic
- Statement aggregation from card transactions

---

### 4.5 Payment Service (:5004)

#### Purpose
Manages payments with OTP 2FA, wallet operations, and saga orchestration.

#### Domain Entities
- **Payment**: Id, UserId, CardId, BillId, Amount, PaymentType, Status, FailureReason, OtpCode, OtpExpiresAtUtc, CreatedAtUtc, UpdatedAtUtc
- **Transaction**: Id, PaymentId, UserId, Amount, Type, Description, CreatedAtUtc, UpdatedAtUtc
- **UserWallet**: Id, UserId, Balance, TotalTopUps, TotalSpent, CreatedAtUtc, UpdatedAtUtc
- **WalletTransaction**: Id, WalletId, Type, Amount, BalanceAfter, Description, RelatedPaymentId, CreatedAtUtc
- **PaymentOrchestrationSagaState**: CorrelationId, CurrentState, UserId, CardId, BillId, Email, FullName, Amount, RewardsAmount, PaymentType, CompensationReason, error fields
- **RazorpayWalletTopUp**: Id, UserId, Amount, RazorpayOrderId, RazorpayPaymentId, RazorpaySignature, Status, FailureReason, CreatedAtUtc, UpdatedAtUtc

#### Application Commands
- `InitiatePaymentCommand` - UserId, CardId, BillId, Amount, PaymentType → validates, creates Payment, generates OTP, publishes IPaymentOtpGenerated
- `VerifyPaymentOtpCommand` - PaymentId, OTP → validates OTP, publishes IStartPaymentOrchestration
- `ResendPaymentOtpCommand` - PaymentId → regenerates OTP
- `CreateWalletCommand` - UserId → initializes wallet (auto-created on first use)
- `TopUpWalletCommand` - UserId, Amount → adds to wallet balance
- `ProcessRazorpayWebhookCommand` - Razorpay payload → validates signature, updates top-up
- `DebitWalletCommand` - UserId, Amount → deducts from wallet (saga step)
- `RefundWalletCommand` - UserId, Amount → refunds to wallet (compensation)

#### Application Queries
- `GetPaymentByIdQuery` - PaymentId → returns payment
- `GetPaymentTransactionsQuery` - PaymentId → returns payment transactions
- `GetWalletQuery` - UserId → returns wallet info
- `GetWalletBalanceQuery` - UserId → returns balance
- `GetWalletTransactionsQuery` - UserId → returns wallet history

#### Saga State Machine
- `PaymentOrchestrationSaga` - MassTransit state machine (see Section 14)

#### Events Published
- `IPaymentOtpGenerated` - OTP generated for payment
- `IStartPaymentOrchestration` - Triggers saga
- Saga state transition events

#### Background Jobs
- `PaymentExpirationJob` - Expires unpaid payments after timeout
- `RazorpayWebhookHandler` - Processes Razorpay callbacks

#### Infrastructure
- `PaymentDbContext` - EF Core DbContext for credvault_payments
- Razorpay client for wallet top-ups
- Saga state repository

---

### 4.6 Notification Service (:5005)

#### Purpose
Consumes domain events and sends email notifications; maintains audit trail.

#### Domain Entities
- **AuditLog**: Id, EntityName, EntityId, Action, UserId, Changes (JSON), TraceId, CreatedAtUtc
- **NotificationLog**: Id, UserId, Recipient, Subject, Body, Type, IsSuccess, ErrorMessage, TraceId, CreatedAtUtc

#### Application Commands
- `SendEmailCommand` - Recipient, Subject, Body → sends via Gmail SMTP, logs result
- `LogAuditCommand` - EntityName, EntityId, Action, UserId, Changes → creates audit entry
- `LogNotificationCommand` - UserId, Recipient, Subject, Type, IsSuccess, Error → logs notification

#### Consumers (MassTransit)
- `DomainEventConsumer` - Consumes all domain events from RabbitMQ:
  - `IUserRegistered` → sends welcome email
  - `IUserOtpGenerated` → sends OTP email
  - `ICardAdded` → sends card addition confirmation
  - `IPaymentOtpGenerated` → sends payment OTP email
  - Saga completion/failure events → sends payment result emails

#### Infrastructure
- `NotificationDbContext` - EF Core DbContext for credvault_notifications
- `GmailSmtpEmailSender` - SMTP client for Gmail
- All consumers use retry policy (1s → 5s → 15s)

---

## 5. COMPLETE API REFERENCE

### 5.1 Standard API Response Format

All responses follow this envelope:

```json
{
  "success": true,
  "data": { ... },
  "message": "Operation successful",
  "errors": []
}
```

Error response:

```json
{
  "success": false,
  "data": null,
  "message": "Validation failed",
  "errors": ["Email is required", "Password must be at least 8 characters"]
}
```

### 5.2 Identity Service Endpoints (Base: `/api/v1/identity`)

#### Authentication

| # | Method | Endpoint | Auth | Description |
|---|--------|----------|------|-------------|
| 1 | POST | `/auth/register` | Public | Register new user |
| 2 | POST | `/auth/login` | Public | Email/password login |
| 3 | POST | `/auth/google` | Public | Google OAuth login |
| 4 | POST | `/auth/verify-email-otp` | Public | Verify email with OTP |
| 5 | POST | `/auth/resend-verification` | Public | Resend verification OTP |
| 6 | POST | `/auth/forgot-password` | Public | Request password reset |
| 7 | POST | `/auth/reset-password` | Public | Reset password with OTP |

#### User Profile (Authenticated)

| # | Method | Endpoint | Auth | Description |
|---|--------|----------|------|-------------|
| 8 | GET | `/users/me` | User | Get current user profile |
| 9 | PUT | `/users/me` | User | Update current user profile |
| 10 | PUT | `/users/me/password` | User | Change password |

#### Admin User Management

| # | Method | Endpoint | Auth | Description |
|---|--------|----------|------|-------------|
| 11 | GET | `/users` | Admin | List all users (paginated) |
| 12 | GET | `/users/{id}` | Admin | Get user by ID |
| 13 | PUT | `/users/{id}/status` | Admin | Update user status |
| 14 | PUT | `/users/{id}/role` | Admin | Update user role |
| 15 | GET | `/users/stats` | Admin | User statistics |

### 5.3 Card Service Endpoints (Base: `/api/v1/cards`)

#### Card Management

| # | Method | Endpoint | Auth | Description |
|---|--------|----------|------|-------------|
| 16 | GET | `/cards` | User | List user's cards |
| 17 | POST | `/cards` | User | Add new card |
| 18 | GET | `/cards/{id}` | User | Get card details |
| 19 | PUT | `/cards/{id}` | User | Update card |
| 20 | PATCH | `/cards/{id}/default` | User | Set card as default |
| 21 | DELETE | `/cards/{id}` | User | Soft-delete card |

#### Card Transactions

| # | Method | Endpoint | Auth | Description |
|---|--------|----------|------|-------------|
| 22 | GET | `/cards/{cardId}/transactions` | User | Get card transactions |
| 23 | POST | `/cards/{cardId}/transactions` | User | Record card transaction |
| 24 | GET | `/cards/transactions` | User | All transactions for user |

#### Card Issuers

| # | Method | Endpoint | Auth | Description |
|---|--------|----------|------|-------------|
| 25 | GET | `/issuers` | User | List all issuers |
| 26 | GET | `/issuers/{id}` | User | Get issuer details |
| 27 | POST | `/issuers` | Admin | Create issuer |
| 28 | PUT | `/issuers/{id}` | Admin | Update issuer |
| 29 | DELETE | `/issuers/{id}` | Admin | Delete issuer |

#### Admin Card Management

| # | Method | Endpoint | Auth | Description |
|---|--------|----------|------|-------------|
| 30 | GET | `/cards/admin` | Admin | List all cards (paginated) |
| 31 | GET | `/cards/{id}/admin` | Admin | Get card details (admin) |
| 32 | PUT | `/cards/{id}/admin` | Admin | Update card (admin) |
| 33 | GET | `/cards/admin/{id}/transactions` | Admin | Get card transactions (admin) |
| 34 | GET | `/cards/user/{userId}` | User | Get cards by user ID |

### 5.4 Billing Service Endpoints (Base: `/api/v1/billing`)

#### Bills

| # | Method | Endpoint | Auth | Description |
|---|--------|----------|------|-------------|
| 35 | GET | `/bills` | User | List user's bills |
| 36 | GET | `/bills/{id}` | User | Get bill details |
| 37 | GET | `/bills/has-pending/{cardId}` | User | Check if card has pending bill |
| 38 | POST | `/bills/admin/generate-bill` | Admin | Generate bill for card |
| 39 | POST | `/bills/admin/check-overdue` | Admin | Check and mark overdue bills |

#### Statements

| # | Method | Endpoint | Auth | Description |
|---|--------|----------|------|-------------|
| 40 | GET | `/statements` | User | List user's statements |
| 41 | GET | `/statements/{id}` | User | Get statement details |
| 42 | GET | `/statements/{id}/transactions` | User | Get statement transactions |

#### Rewards

| # | Method | Endpoint | Auth | Description |
|---|--------|----------|------|-------------|
| 43 | GET | `/rewards/account` | User | Get reward account (balance) |
| 44 | GET | `/rewards/transactions` | User | Get reward transaction history |
| 45 | POST | `/rewards/internal/redeem` | Internal | Redeem rewards (saga step) |
| 46 | GET | `/rewards/tiers` | User | List reward tiers |
| 47 | POST | `/rewards/tiers` | Admin | Create reward tier |
| 48 | PUT | `/rewards/tiers/{id}` | Admin | Update reward tier |
| 49 | DELETE | `/rewards/tiers/{id}` | Admin | Delete reward tier |

### 5.5 Payment Service Endpoints (Base: `/api/v1/payments`)

#### Payments

| # | Method | Endpoint | Auth | Description |
|---|--------|----------|------|-------------|
| 50 | POST | `/payments/initiate` | User | Initiate payment (triggers OTP) |
| 51 | POST | `/payments/{id}/verify-otp` | User | Verify payment OTP |
| 52 | POST | `/payments/{id}/resend-otp` | User | Resend payment OTP |
| 53 | GET | `/payments/{id}/transactions` | User | Get payment transactions |

#### Wallet

| # | Method | Endpoint | Auth | Description |
|---|--------|----------|------|-------------|
| 54 | GET | `/wallets/me` | User | Get wallet info |
| 55 | GET | `/wallets/balance` | User | Get wallet balance |
| 56 | GET | `/wallets/transactions` | User | Get wallet transaction history |

### 5.6 Notification Service Endpoints (Base: `/api/v1/notifications`)

| # | Method | Endpoint | Auth | Description |
|---|--------|----------|------|-------------|
| 57 | GET | `/logs` | Admin | Get notification logs |
| 58 | GET | `/audit` | Admin | Get audit trail |

---

## 6. ENTITY RELATIONSHIP (ER) DIAGRAMS

### 6.1 credvault_identity Database

```
┌──────────────────────────────────────────────────────────┐
│                    identity_users                         │
├──────────────────────────────────────────────────────────┤
│ PK  Id                    UNIQUEIDENTIFIER               │
│ UQ  Email                   NVARCHAR(256)                │
│     FullName                NVARCHAR(200)                │
│     PasswordHash (nullable) NVARCHAR(512)                │
│     IsEmailVerified         BIT                          │
│     EmailVerificationOtp    NVARCHAR(6)                  │
│     PasswordResetOtp        NVARCHAR(6)                  │
│     Status                  INT (enum)                   │
│     Role                    INT (enum)                   │
│     CreatedAtUtc            DATETIME2                    │
│     UpdatedAtUtc            DATETIME2                    │
└──────────────────────────────────────────────────────────┘
```

### 6.2 credvault_cards Database

```
┌──────────────────┐         ┌──────────────────────────────────────────────┐
│   CardIssuers    │         │               CreditCards                     │
├──────────────────┤         ├──────────────────────────────────────────────┤
│ PK  Id           │───────1│N│ PK  Id                                      │
│     Name         │         │     UserId (indexed)                         │
│     Network (int)│         │ FK  IssuerId → CardIssuers.Id                │
│     CreatedAtUtc │         │     CardholderName                           │
│     UpdatedAtUtc │         │     Last4                                    │
└──────────────────┘         │     MaskedNumber                             │
                             │     EncryptedCardNumber                      │
┌──────────────────────────┐ │     ExpMonth                                 │
│   CardTransactions       │ │     ExpYear                                  │
├──────────────────────────┤ │     CreditLimit                              │
│ PK  Id                   │ │     OutstandingBalance                       │
│ FK  CardId → CreditCards │ │     BillingCycleStartDay                     │
│     UserId               │ │     IsDefault                                │
│     Type (int)           │ │     IsVerified                               │
│     Amount               │ │     IsDeleted (soft delete)                  │
│     Description          │ │     CreatedAtUtc                             │
│     DateUtc              │ │     UpdatedAtUtc                             │
└──────────────────────────┘ └──────────────────────────────────────────────┘

Relationships:
  CardIssuers 1──N CreditCards
  CreditCards 1──N CardTransactions
```

### 6.3 credvault_billing Database

```
┌──────────────────┐         ┌──────────────────────────────────────────────┐
│   RewardTiers    │         │                   Bills                       │
├──────────────────┤         ├──────────────────────────────────────────────┤
│ PK  Id           │        1│N│ PK  Id                                      │
│     CardNetwork  │         │     UserId                                   │
│     IssuerId (N) │         │     CardId                                   │
│     MinSpend     │         │     CardNetwork                              │
│     RewardRate   │         │     IssuerId                                 │
│     EffectiveFrom│         │     Amount                                   │
│     EffectiveTo  │         │     MinDue                                   │
│     CreatedAtUtc │         │     Currency                                 │
│     UpdatedAtUtc │         │     BillingDateUtc                           │
└────────┬─────────┘         │     DueDateUtc                               │
         │                   │     AmountPaid                               │
         │                   │     PaidAtUtc                                │
         │                  1│N│     Status (int)                           │
         │                   │     CreatedAtUtc                             │
         │                   │     UpdatedAtUtc                             │
         │                   └──────────────────────────────────────────────┘
         │
         │                   ┌──────────────────────────────────────────────┐
         │                   │                Statements                     │
         │                   ├──────────────────────────────────────────────┤
         │                  1│N│ PK  Id                                      │
         │                   │     UserId                                   │
         │                   │     CardId                                   │
         │                   │ FK  BillId → Bills.Id                        │
         │                   │     StatementPeriod                          │
         │                   │     PeriodStartUtc                           │
         │                   │     PeriodEndUtc                             │
         │                   │     OpeningBalance                           │
         │                   │     TotalPurchases/Payments/Refunds          │
         │                   │     PenaltyCharges                           │
         │                   │     InterestCharges                          │
         │                   │     ClosingBalance                           │
         │                   │     MinimumDue                               │
         │                   │     AmountPaid                               │
         │                   │     Status                                   │
         │                   │     CardLast4, CardNetwork, IssuerName       │
         │                   │     CreditLimit, AvailableCredit             │
         │                   │     CreatedAtUtc, UpdatedAtUtc               │
         │                   └──────────────────┬───────────────────────────┘
         │                                      │
         │                   ┌──────────────────▼───────────────────────────┐
         │                   │          StatementTransactions               │
         │                   ├──────────────────────────────────────────────┤
         │                   │ PK  Id                                       │
         │                  1│N│ FK  StatementId → Statements.Id            │
         │                   │     SourceTransactionId                      │
         │                   │     Type                                     │
         │                   │     Amount                                   │
         │                   │     Description                              │
         │                   │     DateUtc                                  │
         │                   └──────────────────────────────────────────────┘
         │
         │                   ┌──────────────────────────────────────────────┐
         │                   │            RewardAccounts                     │
         │                   ├──────────────────────────────────────────────┤
         │                   │ PK  Id                                       │
         │                   │ UQ  UserId (unique)                          │
         │                  N│1│ FK  RewardTierId → RewardTiers.Id          │
         │                   │     PointsBalance                            │
         │                   │     CreatedAtUtc, UpdatedAtUtc               │
         │                   └──────────────────┬───────────────────────────┘
         │                                      │
         │                   ┌──────────────────▼───────────────────────────┐
         │                   │          RewardTransactions                  │
         │                   ├──────────────────────────────────────────────┤
         │                   │ PK  Id                                       │
         │                  1│N│ FK  RewardAccountId → RewardAccounts.Id    │
         │                   │ FK  BillId → Bills.Id (nullable)             │
         │                   │     Points                                   │
         │                   │     Type (Earned/Redeemed/Reversed)          │
         │                   │     CreatedAtUtc                             │
         │                   └──────────────────────────────────────────────┘

Relationships:
  RewardTiers 1──N RewardAccounts
  Bills 1──N Statements
  Statements 1──N StatementTransactions
  RewardAccounts 1──N RewardTransactions
  Bills 1──N RewardTransactions (nullable FK)
```

### 6.4 credvault_payments Database

```
┌──────────────────┐         ┌──────────────────────────────────────────────┐
│   UserWallets    │         │                Payments                       │
├──────────────────┤         ├──────────────────────────────────────────────┤
│ PK  Id           │         │ PK  Id                                       │
│ UQ  UserId (uniq)│         │     UserId                                   │
│     Balance      │         │     CardId                                   │
│     TotalTopUps  │         │     BillId                                   │
│     TotalSpent   │         │     Amount                                   │
│     CreatedAtUtc │         │     PaymentType (Wallet/Card)                │
│     UpdatedAtUtc │         │     Status                                   │
└────────┬─────────┘         │     FailureReason                            │
         │                   │     OtpCode                                  │
         │                   │     OtpExpiresAtUtc                          │
         │                   │     CreatedAtUtc, UpdatedAtUtc               │
         │                   └──────────────────┬───────────────────────────┘
         │                                      │
┌────────▼─────────┐         ┌──────────────────▼───────────────────────────┐
│ WalletTransactions│        │               Transactions                    │
├──────────────────┤         ├──────────────────────────────────────────────┤
│ PK  Id           │        1│N│ PK  Id                                     │
│ FK  WalletId     │         │ FK  PaymentId → Payments.Id                  │
│     Type         │         │     UserId                                   │
│     Amount       │         │     Amount                                   │
│     BalanceAfter │         │     Type (Debit/Credit)                      │
│     Description  │         │     Description                              │
│ RelatedPaymentId │         │     CreatedAtUtc, UpdatedAtUtc               │
│     CreatedAtUtc │         └──────────────────────────────────────────────┘
└──────────────────┘

┌──────────────────────────────────────────────────────────┐
│         PaymentOrchestrationSagas                        │
├──────────────────────────────────────────────────────────┤
│ PK  CorrelationId          UNIQUEIDENTIFIER               │
│     CurrentState           NVARCHAR(50)                   │
│     UserId                 UNIQUEIDENTIFIER               │
│     CardId                 UNIQUEIDENTIFIER               │
│     BillId                 UNIQUEIDENTIFIER               │
│     Email                  NVARCHAR(256)                  │
│     FullName               NVARCHAR(200)                  │
│     Amount                 DECIMAL(18,2)                  │
│     RewardsAmount          DECIMAL(18,2)                  │
│     PaymentType            INT                            │
│     CompensationReason     NVARCHAR(500)                  │
│     Error fields...                                       │
└──────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│            RazorpayWalletTopUps                          │
├──────────────────────────────────────────────────────────┤
│ PK  Id                     UNIQUEIDENTIFIER               │
│     UserId                 UNIQUEIDENTIFIER               │
│     Amount                 DECIMAL(18,2)                  │
│ UQ  RazorpayOrderId        NVARCHAR(100)                  │
│ UQ  RazorpayPaymentId      NVARCHAR(100)                  │
│     RazorpaySignature      NVARCHAR(512)                  │
│     Status                 INT                            │
│     FailureReason          NVARCHAR(500)                  │
│     CreatedAtUtc, UpdatedAtUtc                            │
└──────────────────────────────────────────────────────────┘

Relationships:
  UserWallets 1──N WalletTransactions
  Payments 1──N Transactions
  (Sagas and TopUps are independent tracking tables)
```

### 6.5 credvault_notifications Database

```
┌──────────────────────────────────────────────────────────┐
│                    AuditLogs                              │
├──────────────────────────────────────────────────────────┤
│ PK  Id                     UNIQUEIDENTIFIER               │
│     EntityName             NVARCHAR(100)                  │
│     EntityId               UNIQUEIDENTIFIER               │
│     Action                 NVARCHAR(50)                   │
│     UserId                 UNIQUEIDENTIFIER               │
│     Changes                NVARCHAR(MAX) (JSON)           │
│     TraceId                NVARCHAR(100)                  │
│     CreatedAtUtc           DATETIME2                      │
└──────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│                  NotificationLogs                         │
├──────────────────────────────────────────────────────────┤
│ PK  Id                     UNIQUEIDENTIFIER               │
│     UserId                 UNIQUEIDENTIFIER               │
│     Recipient              NVARCHAR(256)                  │
│     Subject                NVARCHAR(500)                  │
│     Body                   NVARCHAR(MAX)                  │
│     Type                   NVARCHAR(10) (Email/SMS)       │
│     IsSuccess              BIT                            │
│     ErrorMessage           NVARCHAR(1000)                 │
│     TraceId                NVARCHAR(100)                  │
│     CreatedAtUtc           DATETIME2                      │
└──────────────────────────────────────────────────────────┘
```

---

## 7. SEQUENCE DIAGRAMS

### 7.1 User Registration Flow

```
User            Angular         Gateway        Identity Svc      RabbitMQ       Notification Svc    Gmail SMTP    Identity DB
 │                │               │                │                │                  │               │              │
 │──Register─────>│               │                │                │                  │               │              │
 │  (email,name,  │               │                │                │                  │               │              │
 │   password)    │               │                │                │                  │               │              │
 │                │──POST /auth/─>│                │                │                  │               │              │
 │                │  register     │                │                │                  │               │              │
 │                │               │──forward───────>│                │                  │               │              │
 │                │               │                │──Create User──>│                  │               │              │
 │                │               │                │  (PendingVer.) │                  │               │              │
 │                │               │                │                │──Publish────────>│               │              │
 │                │               │                │                │  IUserRegistered │               │              │
 │                │               │                │                │  IUserOtpGen.    │               │              │
 │                │               │                │<──Save User────│                  │               │              │
 │                │               │                │                │                  │──Consume──────>│              │
 │                │               │                │                │                  │  events        │              │
 │                │               │                │                │                  │──Send Email────────────────────>│
 │                │               │                │                │                  │<──OK───────────│              │
 │                │               │                │                │                  │──Log Notification│             │
 │                │               │                │                │                  │  (DB)          │              │
 │                │               │<──200 (pending)│                │                  │               │              │
 │                │<──Response────│               │                │                  │               │              │
 │<──Show OTP──── │               │                │                │                  │               │              │
 │  Input Screen  │               │                │                │                  │               │              │
 │                │               │                │                │                  │               │              │
 │──Submit OTP───>│               │                │                │                  │               │              │
 │                │──POST /verify─>│                │                │                  │               │              │
 │                │  -email-otp    │                │                │                  │               │              │
 │                │               │──forward───────>│                │                  │               │              │
 │                │               │                │──Verify OTP───>│                  │               │              │
 │                │               │                │  Mark verified │                  │               │              │
 │                │               │                │  Generate JWT  │                  │               │              │
 │                │               │<──200 + JWT────│                │                  │               │              │
 │                │<──Response────│               │                │                  │               │              │
 │<──Login + JWT─ │               │                │                │                  │               │              │
 │  Store in      │               │                │                │                  │               │              │
 │  sessionStorage│               │                │                │                  │               │              │
```

### 7.2 Bill Payment Flow (with Saga)

```
User            Angular         Gateway        Payment Svc      RabbitMQ       Billing Svc     Card Svc     Notification Svc
 │                │               │                │                │                │              │                │
 │──Pay Bill─────>│               │                │                │                │              │                │
 │  (select bill, │               │                │                │                │              │                │
 │   amount,      │               │                │                │                │              │                │
 │   pay method)  │               │                │                │                │              │                │
 │                │──POST /pay    │               │                │                │              │                │
 │                │  ments/init.  │               │                │                │              │                │
 │                │               │──forward──────>│                │                │              │                │
 │                │               │                │──Validate bill │                │              │                │
 │                │               │                │──Create Payment│                │              │                │
 │                │               │                │──Generate OTP  │                │              │                │
 │                │               │                │                │──Publish──────>│              │                │
 │                │               │                │                │  IPaymentOtp   │              │                │
 │                │               │<──200 (OTP sent│                │                │              │                │
 │                │<──Prompt OTP──│               │                │                │              │                │
 │                │               │                │                │──Consume───────────────────────────────>│       │
 │                │               │                │                │                │              │──Send OTP Email│
 │                │               │                │                │                │              │                │
 │──Submit OTP───>│               │                │                │                │              │                │
 │                │──POST /pay    │               │                │                │              │                │
 │                │  ments/{id}   │               │                │                │              │                │
 │                │  /verify-otp  │               │                │                │              │                │
 │                │               │──forward──────>│                │                │              │                │
 │                │               │                │──Verify OTP    │                │              │                │
 │                │               │                │                │──Publish──────>│              │                │
 │                │               │                │                │  IStartPayOrch │              │                │
 │                │               │<──200 (processing)              │                │              │                │
 │                │<──"Processing"│               │                │                │              │                │
 │                │               │                │                │                │              │                │
 │                │               │                │◄── Saga State Machine Executes ──────────────────────────────│
 │                │               │                │                │                │              │                │
 │                │               │                │──Saga:         │──Request──────>│              │                │
 │                │               │                │  Update Bill   │  UpdateBill    │              │                │
 │                │               │                │                │<──Bill Updated │              │                │
 │                │               │                │                │                │              │                │
 │                │               │                │──Saga:         │──Request──────>│              │                │
 │                │               │                │  Redeem Reward │  RedeemRewards │              │                │
 │                │               │                │  (if > 0)      │<──Rewards Done │              │                │
 │                │               │                │                │                │              │                │
 │                │               │                │──Saga:         │──Request─────────────────────>│                │
 │                │               │                │  Deduct Card   │  DeductCard    │              │                │
 │                │               │                │                │<──Card Updated │              │                │
 │                │               │                │                │                │              │                │
 │                │               │                │──Saga: Complete│──Publish──────>│              │                │
 │                │               │                │  Status        │  PaymentDone   │              │                │
 │                │               │                │                │                │              │──Send Conf.   │
 │                │               │                │                │                │              │   Email       │
 │                │               │                │                │                │              │                │
 │<──Payment OK── │<──Poll/WS──── │<──200 (done)── │                │                │              │                │
```

### 7.3 Saga Compensation (Failure) Flow

```
Payment Svc     RabbitMQ       Billing Svc     Card Svc     UserWallet
    │              │               │              │              │
    │──Saga fails │               │              │              │
    │  at any step│               │              │              │
    │              │               │              │              │
    │──Compensate─>│               │              │              │
    │              │               │              │              │
    │  (if bill    │──Revert──────>│              │              │
    │   was updated│  BillUpdate   │              │              │
    │              │<──Reverted    │              │              │
    │              │               │              │              │
    │  (if wallet  │──────────────────────────────>│              │
    │   debited)   │  RefundWallet │              │              │
    │              │<──Refunded    │              │              │
    │              │               │              │              │
    │──Mark Failed │               │              │              │
    │  Compensated │               │              │              │
    │              │──Publish─────>│              │              │
    │              │  Compensated  │              │              │
    │              │               │              │              │
    │              │────────────────────────────────────────────>│
    │              │  Consume (send failure email)               │
```

### 7.4 Card Addition Flow

```
User            Angular         Gateway        Card Svc        RabbitMQ      Notification Svc    Gmail SMTP
 │                │               │                │               │                │               │
 │──Add Card─────>│               │                │                │                │               │
 │  (card number, │               │                │                │                │               │
 │   expiry,      │               │                │                │                │               │
 │   issuer,      │               │                │                │                │               │
 │   limit)       │               │                │                │                │               │
 │                │──POST /cards──>│                │                │                │               │
 │                │               │──forward──────>│                │                │               │
 │                │               │                │──Encrypt card # │               │               │
 │                │               │                │──Create Card    │               │               │
 │                │               │                │                │──Publish──────>│               │
 │                │               │                │                │  ICardAdded    │               │
 │                │               │                │                │                │──Consume─────>│
 │                │               │                │                │                │──Send Email───────────────────>│
 │                │               │                │                │                │<──OK─────────│               │
 │                │               │                │                │                │──Log         │               │
 │                │               │<──201 + Card   │                │                │   (DB)       │               │
 │                │<──Card Added── │               │                │                │               │
```

### 7.5 Google OAuth Login Flow

```
User            Angular         Gateway        Identity Svc     Google
 │                │               │                │               │
 │──Login with   │               │                │               │
 │  Google       │               │                │               │
 │                │──Google Auth─>│               │               │
 │                │  Flow         │               │               │
 │                │<──Google      │               │               │
 │                │   IdToken     │               │               │
 │                │               │                │               │
 │                │──POST /auth/─>│                │               │
 │                │  google       │                │               │
 │                │  (IdToken)    │                │               │
 │                │               │──forward──────>│               │
 │                │               │                │──Validate     │
 │                │               │                │  IdToken with │
 │                │               │                │  Google       │
 │                │               │                │<──Validate────────────>│
 │                │               │                │               │
 │                │               │                │──User exists? │
 │                │               │                │  Yes → Login  │
 │                │               │                │  No → Create +│
 │                │               │                │       Login   │
 │                │               │                │──Generate JWT │
 │                │               │<──200 + JWT────│               │
 │                │<──Response────│               │               │
 │<──Logged in + │               │                │               │
 │  JWT stored   │               │                │               │
```

### 7.6 Wallet Top-Up via Razorpay Flow

```
User            Angular         Gateway        Payment Svc      Razorpay
 │                │               │                │               │
 │──Top Up       │               │                │               │
 │  Wallet       │               │                │               │
 │                │──GET /wallets│               │               │
 │                │  /balance    │               │               │
 │                │               │──forward──────>│               │
 │                │               │<──Balance      │               │
 │                │<──Balance     │               │               │
 │                │               │                │               │
 │                │──Init Top-Up─>│               │               │
 │                │  (amount)     │               │               │
 │                │               │──Create Razor │               │
 │                │               │  pay Order    │──Create Order──────────>│
 │                │               │<──Order ID────│<──Order Created───────│
 │                │<──Order ID    │               │               │
 │                │               │                │               │
 │                │──Razorpay     │               │               │
 │                │  Checkout     │               │               │
 │                │<──Razorpay    │               │               │
 │                │  UI           │               │               │
 │                │──User pays───>│               │               │
 │                │               │                │               │
 │                │               │                │──Webhook      │
 │                │               │                │<──Payment────────────>│
 │                │               │                │  Callback     │
 │                │               │                │──Verify       │
 │                │               │                │  Signature    │
 │                │               │                │──Update Wallet│
 │                │               │                │──Mark Top-Up  │
 │                │               │                │  Complete     │
 │                │               │                │               │
 │<──Top Up Done  │<──WS/Poll     │<──200          │               │
```

---

## 8. USE CASE SPECIFICATIONS

### 8.1 Actors

| Actor | Description |
|-------|-------------|
| **Guest** | Unregistered user, can register or login |
| **Registered User** | Authenticated user, can manage cards, pay bills, view statements, use wallet |
| **Admin** | User with admin role, can manage all users, cards, bills, rewards, view logs |
| **System** | Automated processes (bill generation, payment expiration, saga orchestration) |
| **Razorpay** | External payment gateway for wallet top-ups |
| **Google** | External OAuth provider for SSO |
| **Gmail SMTP** | External email service for notifications |

### 8.2 Use Case: User Registration

| Field | Value |
|-------|-------|
| **ID** | UC-001 |
| **Name** | User Registration |
| **Actor** | Guest |
| **Precondition** | User does not have an account |
| **Trigger** | User clicks "Register" and submits form |

**Main Flow:**
1. User enters email, full name, password
2. System validates input (email format, password strength)
3. System checks email uniqueness
4. System creates user with status "PendingVerification"
5. System generates 6-digit OTP
6. System publishes `IUserRegistered` and `IUserOtpGenerated` events
7. Notification service sends welcome email with OTP
8. System returns success response

**Alternate Flows:**
- A1: Email already exists → Return validation error
- A2: Invalid email format → Return validation error
- A3: Weak password → Return validation error

**Postcondition:** User record created in pending state; OTP email sent

---

### 8.3 Use Case: Email Verification

| Field | Value |
|-------|-------|
| **ID** | UC-002 |
| **Name** | Email Verification |
| **Actor** | Guest (registered but unverified) |
| **Precondition** | User has registered but not verified email |
| **Trigger** | User submits OTP from email |

**Main Flow:**
1. User enters email and OTP
2. System validates OTP matches and is not expired
3. System marks user as email verified
4. System generates JWT token
5. System returns JWT token

**Alternate Flows:**
- A1: Invalid OTP → Return error, allow retry
- A2: Expired OTP → Return error, require resend

**Postcondition:** User email verified; JWT issued

---

### 8.4 Use Case: User Login

| Field | Value |
|-------|-------|
| **ID** | UC-003 |
| **Name** | User Login (Email/Password) |
| **Actor** | Guest |
| **Precondition** | User has verified account |
| **Trigger** | User submits login credentials |

**Main Flow:**
1. User enters email and password
2. System finds user by email
3. System validates password hash
4. System checks user status is "Active"
5. System generates JWT token
6. System returns JWT token

**Alternate Flows:**
- A1: Email not found → Return generic error
- A2: Invalid password → Return generic error
- A3: Account not verified → Return "verify email first" error
- A4: Account suspended → Return "account suspended" error

**Postcondition:** JWT issued to user

---

### 8.5 Use Case: Add Credit Card

| Field | Value |
|-------|-------|
| **ID** | UC-004 |
| **Name** | Add Credit Card |
| **Actor** | Registered User |
| **Precondition** | User is authenticated; issuer exists |
| **Trigger** | User submits card details |

**Main Flow:**
1. User enters card number, cardholder name, expiry, issuer, credit limit
2. System validates input
3. System encrypts card number
4. System generates masked number (last 4 digits visible)
5. System saves card to database
6. System publishes `ICardAdded` event
7. Notification service sends confirmation email
8. System returns card details (masked)

**Alternate Flows:**
- A1: Invalid card number → Return validation error
- A2: Expired card → Return validation error
- A3: Duplicate card → Return error

**Postcondition:** Card added; confirmation email sent

---

### 8.6 Use Case: Generate Bill

| Field | Value |
|-------|-------|
| **ID** | UC-005 |
| **Name** | Generate Bill |
| **Actor** | Admin / System |
| **Precondition** | Card has transactions since last billing |
| **Trigger** | Admin triggers generation or scheduled job runs |

**Main Flow:**
1. System aggregates all transactions for the card in billing period
2. System calculates total purchases, payments, refunds
3. System applies penalty/interest if applicable
4. System calculates minimum due (percentage of total)
5. System creates bill record with status "Pending"
6. System creates statement with transaction breakdown
7. System returns bill details

**Postcondition:** Bill and statement created

---

### 8.7 Use Case: Initiate Payment

| Field | Value |
|-------|-------|
| **ID** | UC-006 |
| **Name** | Initiate Bill Payment |
| **Actor** | Registered User |
| **Precondition** | User authenticated; bill exists and is pending |
| **Trigger** | User selects bill and clicks "Pay" |

**Main Flow:**
1. User selects payment method (Wallet or Card)
2. User enters payment amount
3. System validates bill status (must be Pending or PartiallyPaid)
4. System validates amount (must be >= minimum due)
5. System creates payment record
6. System generates 6-digit OTP
7. System publishes `IPaymentOtpGenerated` event
8. Notification service sends OTP email
9. System returns payment ID

**Alternate Flows:**
- A1: Insufficient wallet balance → Return error (if wallet payment)
- A2: Bill already paid → Return error
- A3: Payment amount invalid → Return validation error

**Postcondition:** Payment record created; OTP sent

---

### 8.8 Use Case: Verify Payment OTP

| Field | Value |
|-------|-------|
| **ID** | UC-007 |
| **Name** | Verify Payment OTP |
| **Actor** | Registered User |
| **Precondition** | Payment initiated; OTP sent |
| **Trigger** | User submits OTP from email |

**Main Flow:**
1. User enters OTP
2. System validates OTP matches and not expired
3. System publishes `IStartPaymentOrchestration` event
4. Saga state machine begins execution
5. System returns "Processing" status
6. Saga executes through all states
7. On success: Bill marked Paid, rewards redeemed, card balance updated
8. On failure: Compensation rollback triggered
9. User polls/checks final payment status

**Alternate Flows:**
- A1: Invalid OTP → Return error
- A2: Expired OTP → Return error, require resend
- A3: Saga failure → Compensation triggered, user notified

**Postcondition:** Payment completed or compensated

---

### 8.9 Use Case: Wallet Top-Up

| Field | Value |
|-------|-------|
| **ID** | UC-008 |
| **Name** | Wallet Top-Up via Razorpay |
| **Actor** | Registered User |
| **Precondition** | User authenticated; wallet exists |
| **Trigger** | User requests wallet top-up |

**Main Flow:**
1. User enters top-up amount
2. System creates Razorpay order
3. System returns Razorpay order ID to frontend
4. Frontend opens Razorpay checkout
5. User completes payment on Razorpay
6. Razorpay sends webhook callback
7. System verifies Razorpay signature
8. System updates wallet balance
9. System creates wallet transaction record
10. System marks top-up as completed

**Alternate Flows:**
- A1: Razorpay payment failed → Mark top-up as failed
- A2: Signature mismatch → Reject webhook
- A3: Duplicate webhook → Ignore (idempotent)

**Postcondition:** Wallet balance updated

---

### 8.10 Use Case: Admin Manage Users

| Field | Value |
|-------|-------|
| **ID** | UC-009 |
| **Name** | Admin User Management |
| **Actor** | Admin |
| **Precondition** | User has admin role |
| **Trigger** | Admin accesses admin panel |

**Main Flow:**
1. Admin views user list (paginated)
2. Admin can view individual user details
3. Admin can change user status (Active/Suspended)
4. Admin can change user role (User/Admin)
5. Admin can view user statistics

**Postcondition:** User status/role updated as requested

---

### 8.11 Use Case: View Statements

| Field | Value |
|-------|-------|
| **ID** | UC-010 |
| **Name** | View Statements |
| **Actor** | Registered User |
| **Precondition** | User authenticated; statements exist |
| **Trigger** | User navigates to statements page |

**Main Flow:**
1. User views list of statements
2. User selects a statement
3. System returns statement details with transaction breakdown
4. System shows: opening balance, purchases, payments, refunds, charges, closing balance
5. User can view statement analytics

**Postcondition:** Statement displayed

---

### 8.12 Use Case: Earn Rewards

| Field | Value |
|-------|-------|
| **ID** | UC-011 |
| **Name** | Earn Rewards on Payment |
| **Actor** | System (triggered during payment saga) |
| **Precondition** | Payment completed; reward tier configured |
| **Trigger** | Payment saga reaches reward redemption step |

**Main Flow:**
1. System identifies user's reward tier based on card network/issuer
2. System calculates reward points (spend amount × reward rate)
3. System adds points to user's reward account
4. System creates reward transaction record (Type: Earned)
5. System updates reward account balance

**Postcondition:** Reward points credited

---

### 8.13 Use Case: Redeem Rewards

| Field | Value |
|-------|-------|
| **ID** | UC-012 |
| **Name** | Redeem Rewards During Payment |
| **Actor** | System (triggered during payment saga) |
| **Precondition** | User has reward points; payment in progress |
| **Trigger** | Saga reaches reward redemption step |

**Main Flow:**
1. System checks user's reward point balance
2. System calculates redeemable amount
3. System deducts points from reward account
4. System creates reward transaction record (Type: Redeemed)
5. System applies reward amount to payment
6. System continues saga

**Compensation (on saga failure):**
1. System reverses reward deduction
2. System creates reward transaction record (Type: Reversed)
3. System restores points to reward account

**Postcondition:** Rewards redeemed (or reversed on failure)

---

## 9. STATE MACHINES

### 9.1 User Status State Machine

```
┌──────────────┐
│ PendingVerif │ ───────────────────────────────────────┐
│ (just reg.)  │                                        │
└──────┬───────┘                                        │
       │ Verify Email OTP                               │
       ▼                                                │
┌──────────────┐                                        │
│   Active     │ ◄──────────────────────────────────────┘
│  (verified)  │
└──────┬───────┘
       │ Admin Suspends
       ▼
┌──────────────┐
│  Suspended   │
└──────┬───────┘
       │ Admin Activates
       ▼
┌──────────────┐
│   Active     │
└──────────────┘
```

### 9.2 Bill Status State Machine

```
┌──────────────┐
│   Pending    │
└──────┬───────┘
       │ Partial Payment
       ▼
┌──────────────┐         ┌──────────────┐
│ PartiallyPaid│────────>│    Paid      │
└──────┬───────┘  Full   └──────────────┘
       │ Payment           ▲
       │ Past Due Date     │ Full Payment
       ▼                   │
┌──────────────┐           │
│   Overdue    │───────────┘
└──────────────┘  Pay
```

### 9.3 Payment Status State Machine

```
┌──────────────┐
│  Initiated   │ ── OTP sent, awaiting verification
└──────┬───────┘
       │ OTP Verified
       ▼
┌──────────────┐
│  Processing  │ ── Saga executing
└──────┬───────┘
       │
    ┌──┴──┐
    ▼     ▼
┌──────┐ ┌──────────┐
│ Paid │ │ Failed   │
└──────┘ └──────────┘
              │
              ▼
       ┌──────────┐
       │Compensated│ ── Rollback complete
       └──────────┘
```

### 9.4 Payment Saga State Machine (Detailed)

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    PAYMENT ORCHESTRATION SAGA                           │
└─────────────────────────────────────────────────────────────────────────┘

┌───────────┐
│  Initial  │
└─────┬─────┘
      │ IStartPaymentOrchestration
      │ (payment initiated, OTP verified)
      ▼
┌──────────────────────┐
│ AwaitingOtpVerification │
│ - OTP already sent    │
│ - Waiting for verify  │
└──────┬───────────────┘
       │ OtpVerified
       ▼
┌──────────────────────┐
│ AwaitingPaymentConfirm│
│ - Payment record      │
│   validated           │
└──────┬───────────────┘
       │ PaymentConfirmed
       ▼
┌──────────────────────┐
│ AwaitingBillUpdate   │
│ - Send command to    │
│   Billing Service    │
│ - Update bill status │
│ - Record payment     │
└──────┬───────────────┘
       │ BillUpdated
       ▼
┌──────────────────────┐     No Rewards
│AwaitingRewardRedempt │────────────┐
│ - Check reward       │            │
│   balance            │            │
│ - Calculate redeem   │            │
│ - Redeem points      │            │
└──────┬───────────────┘            │
       │ RewardsRedeemed            │
       ▼                            ▼
┌──────────────────────┐  ┌──────────────────────┐
│AwaitingCardDeduction │  │ AwaitingCardDeduction│
│ - Deduct from card   │  │ (no reward path)     │
│   outstanding balance│  │                      │
└──────┬───────────────┘  └──────┬───────────────┘
       │ CardDeducted            │ CardDeducted
       ▼                         ▼
┌──────────────────────────────────────────┐
│              Completed                    │
│ - Payment fully processed               │
│ - Bill paid, rewards redeemed,           │
│   card updated                           │
└──────────────────────────────────────────┘

COMPENSATION PATHS (on any failure):
─────────────────────────────────────
At AwaitingBillUpdate:
  → RevertPayment → Compensated

At AwaitingRewardRedemption:
  → RevertBillUpdate → RevertPayment → Compensated

At AwaitingCardDeduction:
  → ReverseRewards → RevertBillUpdate → RevertPayment → Compensated

  OR (if wallet payment):
  → RefundWallet → RevertBillUpdate → RevertPayment → Compensated

Final Compensation State: Compensated / Failed
```

### 9.5 Wallet Transaction Type State Flow

```
WalletTransaction Types:
  ┌────────┐  ┌────────┐  ┌────────┐
  │ TopUp  │  │ Debit  │  │ Refund │
  └────────┘  └────────┘  └────────┘
      │           │           │
      ▼           ▼           ▼
  Balance +   Balance -   Balance +
  (from       (payment    (saga
   Razorpay)   executed)   rollback)
```

---

## 10. EVENT SCHEMA REFERENCE

### 10.1 Identity Events

#### IUserRegistered
```typescript
interface IUserRegistered {
  userId: string;        // UUID
  email: string;
  fullName: string;
  role: string;          // "user" | "admin"
  registeredAt: Date;
}
```

#### IUserOtpGenerated
```typescript
interface IUserOtpGenerated {
  userId: string;
  email: string;
  otp: string;           // 6-digit code
  otpType: string;       // "EmailVerification" | "PasswordReset"
  expiresAt: Date;
}
```

### 10.2 Card Events

#### ICardAdded
```typescript
interface ICardAdded {
  cardId: string;
  userId: string;
  cardLast4: string;
  cardNetwork: string;   // "Visa" | "Mastercard" | "Rupay" | "Amex"
  issuerName: string;
  addedAt: Date;
}
```

### 10.3 Payment Events

#### IPaymentOtpGenerated
```typescript
interface IPaymentOtpGenerated {
  paymentId: string;
  userId: string;
  email: string;
  otp: string;
  amount: number;
  expiresAt: Date;
}
```

#### IStartPaymentOrchestration
```typescript
interface IStartPaymentOrchestration {
  correlationId: string; // UUID - saga correlation
  paymentId: string;
  userId: string;
  cardId: string;
  billId: string;
  email: string;
  fullName: string;
  amount: number;
  rewardsAmount: number;
  paymentType: string;   // "Wallet" | "Card"
}
```

### 10.4 Saga Events (MassTransit State Machine)

```typescript
// State transition events (internal to saga)
interface IStartBillUpdate {
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

interface IStartRewardRedemption {
  correlationId: string;
  userId: string;
  rewardsAmount: number;
  billId: string;
}

interface IRewardsRedeemed {
  correlationId: string;
  userId: string;
  pointsRedeemed: number;
  success: boolean;
}

interface IStartCardDeduction {
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

### 10.5 Event Routing Table

| Event | Publisher | Consumer(s) | Exchange/Queue |
|-------|-----------|-------------|----------------|
| IUserRegistered | Identity Service | Notification Service | identity-events |
| IUserOtpGenerated | Identity Service | Notification Service | identity-events |
| ICardAdded | Card Service | Notification Service | card-events |
| IPaymentOtpGenerated | Payment Service | Notification Service | payment-events |
| IStartPaymentOrchestration | Payment Service | Payment Service (Saga) | payment-saga |
| IStartBillUpdate | Payment Service (Saga) | Billing Service | billing-saga |
| IBillUpdated | Billing Service | Payment Service (Saga) | billing-saga |
| IStartRewardRedemption | Payment Service (Saga) | Billing Service | billing-saga |
| IRewardsRedeemed | Billing Service | Payment Service (Saga) | billing-saga |
| IStartCardDeduction | Payment Service (Saga) | Card Service | card-saga |
| ICardDeducted | Card Service | Payment Service (Saga) | card-saga |
| IPaymentCompleted | Payment Service (Saga) | Notification Service | payment-events |
| IPaymentCompensated | Payment Service (Saga) | Notification Service | payment-events |

---

## 11. ENUM REFERENCE

### 11.1 UserStatus
```csharp
enum UserStatus {
  PendingVerification = 0,  // Just registered, awaiting email verification
  Active = 1,               // Verified and active
  Suspended = 2             // Suspended by admin
}
```

### 11.2 UserRole
```csharp
enum UserRole {
  User = 0,   // Regular user
  Admin = 1   // Administrator
}
```

### 11.3 CardNetwork
```csharp
enum CardNetwork {
  Visa = 0,
  Mastercard = 1,
  Rupay = 2,
  Amex = 3
}
```

### 11.4 CardTransactionType
```csharp
enum CardTransactionType {
  Purchase = 0,
  Payment = 1,
  Refund = 2
}
```

### 11.5 BillStatus
```csharp
enum BillStatus {
  Pending = 0,
  Paid = 1,
  Overdue = 2,
  PartiallyPaid = 3
}
```

### 11.6 StatementStatus
```csharp
enum StatementStatus {
  Generated = 0,
  Finalized = 1
}
```

### 11.7 RewardTransactionType
```csharp
enum RewardTransactionType {
  Earned = 0,     // Points earned from payment
  Redeemed = 1,   // Points redeemed during payment
  Reversed = 2    // Points reversed (saga compensation)
}
```

### 11.8 PaymentType
```csharp
enum PaymentType {
  Wallet = 0,  // Pay from wallet balance
  Card = 1     // Pay directly from card
}
```

### 11.9 PaymentStatus
```csharp
enum PaymentStatus {
  Initiated = 0,
  Processing = 1,
  Paid = 2,
  Failed = 3,
  Compensated = 4
}
```

### 11.10 TransactionType
```csharp
enum TransactionType {
  Debit = 0,
  Credit = 1
}
```

### 11.11 WalletTransactionType
```csharp
enum WalletTransactionType {
  TopUp = 0,    // Wallet funded
  Debit = 1,    // Wallet debited for payment
  Refund = 2    // Wallet refunded (saga compensation)
}
```

### 11.12 SagaState
```csharp
enum SagaState {
  Initial = 0,
  AwaitingOtpVerification = 1,
  AwaitingPaymentConfirmation = 2,
  AwaitingBillUpdate = 3,
  AwaitingRewardRedemption = 4,
  AwaitingCardDeduction = 5,
  Completed = 6,
  Compensating = 7,
  Compensated = 8,
  Failed = 9
}
```

### 11.13 RazorpayTopUpStatus
```csharp
enum RazorpayTopUpStatus {
  Pending = 0,
  Completed = 1,
  Failed = 2
}
```

### 11.14 NotificationType
```csharp
enum NotificationType {
  Email = 0,
  SMS = 1
}
```

---

## 12. DTO SPECIFICATIONS

### 12.1 Request DTOs

#### RegisterUserRequest
```json
{
  "email": "string (required, valid email)",
  "fullName": "string (required, min 2 chars)",
  "password": "string (required, min 8 chars, 1 uppercase, 1 lowercase, 1 digit)"
}
```

#### LoginUserRequest
```json
{
  "email": "string (required)",
  "password": "string (required)"
}
```

#### GoogleLoginRequest
```json
{
  "idToken": "string (required, Google IdToken)"
}
```

#### OtpVerificationRequest
```json
{
  "email": "string (required)",
  "otp": "string (required, 6 digits)"
}
```

#### ResetPasswordRequest
```json
{
  "email": "string (required)",
  "otp": "string (required, 6 digits)",
  "newPassword": "string (required, min 8 chars)"
}
```

#### AddCardRequest
```json
{
  "cardNumber": "string (required, 16 digits)",
  "cardholderName": "string (required)",
  "expiryMonth": "int (required, 1-12)",
  "expiryYear": "int (required, >= current year)",
  "issuerId": "Guid (required)",
  "creditLimit": "decimal (required, > 0)",
  "billingCycleStartDay": "int (required, 1-28)"
}
```

#### InitiatePaymentRequest
```json
{
  "cardId": "Guid (required)",
  "billId": "Guid (required)",
  "amount": "decimal (required, > 0)",
  "paymentType": "int (required, 0=Wallet, 1=Card)",
  "rewardsAmount": "decimal (optional, 0 if not redeeming)"
}
```

#### VerifyPaymentOtpRequest
```json
{
  "otp": "string (required, 6 digits)"
}
```

#### UpdateUserProfileRequest
```json
{
  "fullName": "string (required, min 2 chars)"
}
```

#### ChangePasswordRequest
```json
{
  "oldPassword": "string (required)",
  "newPassword": "string (required, min 8 chars)"
}
```

### 12.2 Response DTOs

#### AuthResponse
```json
{
  "token": "string (JWT)",
  "user": {
    "id": "Guid",
    "email": "string",
    "fullName": "string",
    "role": "string",
    "status": "string"
  }
}
```

#### CardResponse
```json
{
  "id": "Guid",
  "cardholderName": "string",
  "maskedNumber": "string (e.g. **** **** **** 1234)",
  "last4": "string",
  "expMonth": "int",
  "expYear": "int",
  "creditLimit": "decimal",
  "outstandingBalance": "decimal",
  "isDefault": "boolean",
  "isVerified": "boolean",
  "network": "string",
  "issuerName": "string"
}
```

#### BillResponse
```json
{
  "id": "Guid",
  "cardId": "Guid",
  "amount": "decimal",
  "minDue": "decimal",
  "currency": "string (default: INR)",
  "billingDate": "DateTime",
  "dueDate": "DateTime",
  "amountPaid": "decimal",
  "status": "string",
  "cardLast4": "string",
  "cardNetwork": "string",
  "issuerName": "string"
}
```

#### StatementResponse
```json
{
  "id": "Guid",
  "statementPeriod": "string",
  "periodStart": "DateTime",
  "periodEnd": "DateTime",
  "openingBalance": "decimal",
  "totalPurchases": "decimal",
  "totalPayments": "decimal",
  "totalRefunds": "decimal",
  "penaltyCharges": "decimal",
  "interestCharges": "decimal",
  "closingBalance": "decimal",
  "minimumDue": "decimal",
  "amountPaid": "decimal",
  "status": "string",
  "cardLast4": "string",
  "cardNetwork": "string",
  "issuerName": "string",
  "creditLimit": "decimal",
  "availableCredit": "decimal",
  "transactions": ["StatementTransactionResponse[]"]
}
```

#### WalletResponse
```json
{
  "id": "Guid",
  "balance": "decimal",
  "totalTopUps": "decimal",
  "totalSpent": "decimal"
}
```

#### RewardAccountResponse
```json
{
  "id": "Guid",
  "pointsBalance": "decimal",
  "tierName": "string",
  "rewardRate": "decimal"
}
```

#### PaymentResponse
```json
{
  "id": "Guid",
  "amount": "decimal",
  "paymentType": "string",
  "status": "string",
  "createdAt": "DateTime"
}
```

#### UserStatsResponse (Admin)
```json
{
  "totalUsers": "int",
  "activeUsers": "int",
  "pendingVerification": "int",
  "suspendedUsers": "int",
  "adminUsers": "int"
}
```

---

## 13. AUTHENTICATION & AUTHORIZATION

### 13.1 JWT Configuration

```json
{
  "Jwt": {
    "Issuer": "CredVault",
    "Audience": "CredVaultClient",
    "ExpiryMinutes": 60,
    "Key": "<shared-signing-key>"
  }
}
```

### 13.2 JWT Token Payload

```json
{
  "sub": "<userId>",
  "email": "<userEmail>",
  "name": "<fullName>",
  "role": "<user|admin>",
  "iat": "<issued-at-timestamp>",
  "exp": "<expiry-timestamp>",
  "iss": "CredVault",
  "aud": "CredVaultClient"
}
```

### 13.3 Auth Flow Summary

1. **Registration**: POST `/auth/register` → Creates user (PendingVerification)
2. **Email Verification**: POST `/auth/verify-email-otp` → Marks verified, returns JWT
3. **Login**: POST `/auth/login` → Validates credentials, returns JWT
4. **Google Login**: POST `/auth/google` → Validates Google IdToken, returns JWT
5. **Token Usage**: `Authorization: Bearer <token>` header on all requests
6. **Token Validation**: All services validate JWT using shared configuration
7. **Token Expiry**: 401 returned → Frontend auto-logs out

### 13.4 RBAC Matrix

| Endpoint Pattern | Public | User | Admin |
|------------------|--------|------|-------|
| `/auth/*` | Yes | - | Yes |
| `/users/me` | - | Yes | Yes |
| `/users` | - | - | Yes |
| `/cards` | - | Yes | Yes |
| `/cards/admin/*` | - | - | Yes |
| `/bills` | - | Yes | Yes |
| `/bills/admin/*` | - | - | Yes |
| `/payments/*` | - | Yes | Yes |
| `/wallets/*` | - | Yes | Yes |
| `/rewards/*` | - | Yes | Yes |
| `/rewards/tiers` (write) | - | - | Yes |
| `/notifications/*` | - | - | Yes |
| `/issuers` (write) | - | - | Yes |

### 13.5 OTP Configuration

| OTP Type | Length | Expiry | Usage |
|----------|--------|--------|-------|
| Email Verification | 6 digits | 10 minutes | Registration |
| Password Reset | 6 digits | 10 minutes | Forgot password |
| Payment Verification | 6 digits | 5 minutes | Bill payment 2FA |

---

## 14. SAGA ORCHESTRATION DEEP DIVE

### 14.1 Saga Architecture

**Pattern:** Choreography-based Saga using MassTransit State Machine
**Broker:** RabbitMQ
**Reliability:** InMemoryOutbox pattern prevents lost messages
**Retry:** Exponential backoff (1s → 5s → 15s) on all consumers

### 14.2 Saga Definition

```csharp
// MassTransit State Machine Definition
public class PaymentOrchestrationSagaDefinition : SagaDefinition<PaymentOrchestrationSagaState>
{
    public PaymentOrchestrationSagaDefinition()
    {
        // Concurrent message limit
        EndpointDefinition.ConcurrentMessageLimit = 1;
    }
}
```

### 14.3 Saga State Transitions

```
Initial Event: IStartPaymentOrchestration
  → Create saga instance
  → State: AwaitingOtpVerification

Event: OtpVerified (internal)
  → State: AwaitingPaymentConfirmation
  → Send: IStartBillUpdate to Billing Service

Event: IBillUpdated (from Billing Service)
  → State: AwaitingRewardRedemption (if rewardsAmount > 0)
  → OR: State: AwaitingCardDeduction (if no rewards)
  → Send: IStartRewardRedemption to Billing Service

Event: IRewardsRedeemed (from Billing Service)
  → State: AwaitingCardDeduction
  → Send: IStartCardDeduction to Card Service

Event: ICardDeducted (from Card Service)
  → State: Completed
  → Publish: IPaymentCompleted
  → End saga

On Any Failure (Fault<T> or timeout):
  → State: Compensating
  → Execute compensation actions in reverse order
  → State: Compensated
  → Publish: IPaymentCompensated
  → End saga
```

### 14.4 Compensation Actions

| Compensation Step | Action | Target Service |
|-------------------|--------|----------------|
| RevertBillUpdate | Restore bill to previous status, deduct amountPaid | Billing Service |
| ReverseRewards | Restore redeemed points to reward account | Billing Service |
| RefundWallet | Restore debited wallet balance | Payment Service |
| RevertCardDeduction | Restore card outstanding balance | Card Service |
| RevertPayment | Mark payment as Failed | Payment Service |

### 14.5 Saga Timeout Configuration

| Step | Timeout | Action on Timeout |
|------|---------|-------------------|
| OTP Verification | 5 minutes | Mark payment as expired |
| Bill Update | 30 seconds | Trigger compensation |
| Reward Redemption | 30 seconds | Trigger compensation |
| Card Deduction | 30 seconds | Trigger compensation |

### 14.6 Saga Idempotency

- Saga uses `CorrelationId` (GUID) as primary key
- All commands include correlation ID for deduplication
- Idempotency ensured by checking saga state before processing
- Duplicate messages are ignored if saga already completed/compensated

---

## 15. DATABASE SCHEMA (COMPLETE)

### 15.1 Full Table Definitions

#### credvault_identity.dbo.identity_users

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UNIQUEIDENTIFIER | PK, Default NEWID() | User unique identifier |
| Email | NVARCHAR(256) | UNIQUE, NOT NULL | User email address |
| FullName | NVARCHAR(200) | NOT NULL | User's full name |
| PasswordHash | NVARCHAR(512) | NULLABLE | BCrypt password hash (null for SSO) |
| IsEmailVerified | BIT | NOT NULL, Default 0 | Email verification status |
| EmailVerificationOtp | NVARCHAR(6) | NULLABLE | Current email verification OTP |
| PasswordResetOtp | NVARCHAR(6) | NULLABLE | Current password reset OTP |
| Status | INT | NOT NULL, Default 0 | UserStatus enum |
| Role | INT | NOT NULL, Default 0 | UserRole enum |
| CreatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Record creation time |
| UpdatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Last update time |

**Indexes:**
- PK: `PK_identity_users` on `Id`
- UQ: `UQ_identity_users_Email` on `Email`

---

#### credvault_cards.dbo.CardIssuers

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UNIQUEIDENTIFIER | PK, Default NEWID() | Issuer unique identifier |
| Name | NVARCHAR(100) | NOT NULL | Issuer name (e.g., "HDFC Bank") |
| Network | INT | NOT NULL | CardNetwork enum |
| CreatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Record creation time |
| UpdatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Last update time |

---

#### credvault_cards.dbo.CreditCards

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UNIQUEIDENTIFIER | PK, Default NEWID() | Card unique identifier |
| UserId | UNIQUEIDENTIFIER | NOT NULL, Indexed | Owner user ID |
| IssuerId | UNIQUEIDENTIFIER | FK → CardIssuers.Id | Card issuer |
| CardholderName | NVARCHAR(200) | NOT NULL | Name on card |
| Last4 | NVARCHAR(4) | NOT NULL | Last 4 digits |
| MaskedNumber | NVARCHAR(19) | NOT NULL | Masked display (**** **** **** 1234) |
| EncryptedCardNumber | NVARCHAR(MAX) | NOT NULL | AES encrypted full card number |
| ExpMonth | INT | NOT NULL | Expiry month (1-12) |
| ExpYear | INT | NOT NULL | Expiry year |
| CreditLimit | DECIMAL(18,2) | NOT NULL | Credit limit |
| OutstandingBalance | DECIMAL(18,2) | NOT NULL, Default 0 | Current outstanding |
| BillingCycleStartDay | INT | NOT NULL | Day of month for billing |
| IsDefault | BIT | NOT NULL, Default 0 | Default card flag |
| IsVerified | BIT | NOT NULL, Default 0 | Verification status |
| IsDeleted | BIT | NOT NULL, Default 0 | Soft delete flag |
| CreatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Record creation time |
| UpdatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Last update time |

**Indexes:**
- PK: `PK_CreditCards` on `Id`
- IX: `IX_CreditCards_UserId` on `UserId`
- FK: `FK_CreditCards_IssuerId` → `CardIssuers(Id)`
- Filter: `IsDeleted = 0` (global query filter)

---

#### credvault_cards.dbo.CardTransactions

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UNIQUEIDENTIFIER | PK, Default NEWID() | Transaction unique identifier |
| CardId | UNIQUEIDENTIFIER | FK → CreditCards.Id, NOT NULL | Related card |
| UserId | UNIQUEIDENTIFIER | NOT NULL | Owner user ID |
| Type | INT | NOT NULL | CardTransactionType enum |
| Amount | DECIMAL(18,2) | NOT NULL | Transaction amount |
| Description | NVARCHAR(500) | NULLABLE | Transaction description |
| DateUtc | DATETIME2 | NOT NULL | Transaction date |

---

#### credvault_billing.dbo.Bills

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UNIQUEIDENTIFIER | PK, Default NEWID() | Bill unique identifier |
| UserId | UNIQUEIDENTIFIER | NOT NULL | Owner user ID |
| CardId | UNIQUEIDENTIFIER | NOT NULL | Related card |
| CardNetwork | NVARCHAR(50) | NOT NULL | Card network name |
| IssuerId | UNIQUEIDENTIFIER | NOT NULL | Card issuer |
| Amount | DECIMAL(18,2) | NOT NULL | Total bill amount |
| MinDue | DECIMAL(18,2) | NOT NULL | Minimum due amount |
| Currency | NVARCHAR(3) | NOT NULL, Default 'INR' | Currency code |
| BillingDateUtc | DATETIME2 | NOT NULL | Bill generation date |
| DueDateUtc | DATETIME2 | NOT NULL | Payment due date |
| AmountPaid | DECIMAL(18,2) | NOT NULL, Default 0 | Amount paid so far |
| PaidAtUtc | DATETIME2 | NULLABLE | Full payment timestamp |
| Status | INT | NOT NULL, Default 0 | BillStatus enum |
| CreatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Record creation time |
| UpdatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Last update time |

---

#### credvault_billing.dbo.Statements

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UNIQUEIDENTIFIER | PK, Default NEWID() | Statement unique identifier |
| UserId | UNIQUEIDENTIFIER | NOT NULL | Owner user ID |
| CardId | UNIQUEIDENTIFIER | NOT NULL | Related card |
| BillId | UNIQUEIDENTIFIER | FK → Bills.Id, NULLABLE | Related bill |
| StatementPeriod | NVARCHAR(50) | NOT NULL | Period label (e.g., "Mar 2025") |
| PeriodStartUtc | DATETIME2 | NOT NULL | Period start date |
| PeriodEndUtc | DATETIME2 | NOT NULL | Period end date |
| OpeningBalance | DECIMAL(18,2) | NOT NULL | Balance at period start |
| TotalPurchases | DECIMAL(18,2) | NOT NULL | Total purchases in period |
| TotalPayments | DECIMAL(18,2) | NOT NULL | Total payments in period |
| TotalRefunds | DECIMAL(18,2) | NOT NULL | Total refunds in period |
| PenaltyCharges | DECIMAL(18,2) | NOT NULL | Penalty charges applied |
| InterestCharges | DECIMAL(18,2) | NOT NULL | Interest charges applied |
| ClosingBalance | DECIMAL(18,2) | NOT NULL | Balance at period end |
| MinimumDue | DECIMAL(18,2) | NOT NULL | Minimum due for this statement |
| AmountPaid | DECIMAL(18,2) | NOT NULL | Amount paid against this statement |
| Status | INT | NOT NULL | StatementStatus enum |
| CardLast4 | NVARCHAR(4) | NOT NULL | Card last 4 digits |
| CardNetwork | NVARCHAR(50) | NOT NULL | Card network |
| IssuerName | NVARCHAR(100) | NOT NULL | Issuer name |
| CreditLimit | DECIMAL(18,2) | NOT NULL | Card credit limit |
| AvailableCredit | DECIMAL(18,2) | NOT NULL | Available credit |
| CreatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Record creation time |
| UpdatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Last update time |

---

#### credvault_billing.dbo.StatementTransactions

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UNIQUEIDENTIFIER | PK, Default NEWID() | Transaction unique identifier |
| StatementId | UNIQUEIDENTIFIER | FK → Statements.Id, NOT NULL | Related statement |
| SourceTransactionId | UNIQUEIDENTIFIER | NULLABLE | Original transaction ID |
| Type | INT | NOT NULL | CardTransactionType enum |
| Amount | DECIMAL(18,2) | NOT NULL | Transaction amount |
| Description | NVARCHAR(500) | NULLABLE | Description |
| DateUtc | DATETIME2 | NOT NULL | Transaction date |

---

#### credvault_billing.dbo.RewardAccounts

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UNIQUEIDENTIFIER | PK, Default NEWID() | Account unique identifier |
| UserId | UNIQUEIDENTIFIER | UNIQUE, NOT NULL | Owner user ID |
| RewardTierId | UNIQUEIDENTIFIER | FK → RewardTiers.Id, NOT NULL | Current reward tier |
| PointsBalance | DECIMAL(18,2) | NOT NULL, Default 0 | Current points balance |
| CreatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Record creation time |
| UpdatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Last update time |

---

#### credvault_billing.dbo.RewardTiers

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UNIQUEIDENTIFIER | PK, Default NEWID() | Tier unique identifier |
| CardNetwork | NVARCHAR(50) | NOT NULL | Applicable card network |
| IssuerId | UNIQUEIDENTIFIER | NULLABLE | Specific issuer (null = all) |
| MinSpend | DECIMAL(18,2) | NOT NULL | Minimum spend for tier |
| RewardRate | DECIMAL(5,2) | NOT NULL | Points per unit spend |
| EffectiveFromUtc | DATETIME2 | NOT NULL | Tier effective from |
| EffectiveToUtc | DATETIME2 | NULLABLE | Tier effective until |
| CreatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Record creation time |
| UpdatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Last update time |

---

#### credvault_billing.dbo.RewardTransactions

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UNIQUEIDENTIFIER | PK, Default NEWID() | Transaction unique identifier |
| RewardAccountId | UNIQUEIDENTIFIER | FK → RewardAccounts.Id, NOT NULL | Related reward account |
| BillId | UNIQUEIDENTIFIER | FK → Bills.Id, NULLABLE | Related bill |
| Points | DECIMAL(18,2) | NOT NULL | Points amount |
| Type | INT | NOT NULL | RewardTransactionType enum |
| CreatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Record creation time |

---

#### credvault_payments.dbo.Payments

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UNIQUEIDENTIFIER | PK, Default NEWID() | Payment unique identifier |
| UserId | UNIQUEIDENTIFIER | NOT NULL | Owner user ID |
| CardId | UNIQUEIDENTIFIER | NOT NULL | Related card |
| BillId | UNIQUEIDENTIFIER | NOT NULL | Related bill |
| Amount | DECIMAL(18,2) | NOT NULL | Payment amount |
| PaymentType | INT | NOT NULL | PaymentType enum |
| Status | INT | NOT NULL, Default 0 | PaymentStatus enum |
| FailureReason | NVARCHAR(500) | NULLABLE | Failure description |
| OtpCode | NVARCHAR(6) | NOT NULL | Generated OTP code |
| OtpExpiresAtUtc | DATETIME2 | NOT NULL | OTP expiry time |
| CreatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Record creation time |
| UpdatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Last update time |

---

#### credvault_payments.dbo.Transactions

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UNIQUEIDENTIFIER | PK, Default NEWID() | Transaction unique identifier |
| PaymentId | UNIQUEIDENTIFIER | FK → Payments.Id, NOT NULL | Related payment |
| UserId | UNIQUEIDENTIFIER | NOT NULL | Owner user ID |
| Amount | DECIMAL(18,2) | NOT NULL | Transaction amount |
| Type | INT | NOT NULL | TransactionType enum |
| Description | NVARCHAR(500) | NULLABLE | Description |
| CreatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Record creation time |
| UpdatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Last update time |

---

#### credvault_payments.dbo.UserWallets

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UNIQUEIDENTIFIER | PK, Default NEWID() | Wallet unique identifier |
| UserId | UNIQUEIDENTIFIER | UNIQUE, NOT NULL | Owner user ID |
| Balance | DECIMAL(18,2) | NOT NULL, Default 0 | Current wallet balance |
| TotalTopUps | DECIMAL(18,2) | NOT NULL, Default 0 | Lifetime top-up amount |
| TotalSpent | DECIMAL(18,2) | NOT NULL, Default 0 | Lifetime spent amount |
| CreatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Record creation time |
| UpdatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Last update time |

---

#### credvault_payments.dbo.WalletTransactions

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UNIQUEIDENTIFIER | PK, Default NEWID() | Transaction unique identifier |
| WalletId | UNIQUEIDENTIFIER | FK → UserWallets.Id, NOT NULL | Related wallet |
| Type | INT | NOT NULL | WalletTransactionType enum |
| Amount | DECIMAL(18,2) | NOT NULL | Transaction amount |
| BalanceAfter | DECIMAL(18,2) | NOT NULL | Balance after transaction |
| Description | NVARCHAR(500) | NULLABLE | Description |
| RelatedPaymentId | UNIQUEIDENTIFIER | NULLABLE | Related payment ID |
| CreatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Record creation time |

---

#### credvault_payments.dbo.PaymentOrchestrationSagas

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| CorrelationId | UNIQUEIDENTIFIER | PK | Saga correlation ID |
| CurrentState | NVARCHAR(50) | NOT NULL | Current saga state |
| UserId | UNIQUEIDENTIFIER | NOT NULL | Owner user ID |
| CardId | UNIQUEIDENTIFIER | NOT NULL | Related card |
| BillId | UNIQUEIDENTIFIER | NOT NULL | Related bill |
| Email | NVARCHAR(256) | NOT NULL | User email |
| FullName | NVARCHAR(200) | NOT NULL | User name |
| Amount | DECIMAL(18,2) | NOT NULL | Payment amount |
| RewardsAmount | DECIMAL(18,2) | NOT NULL | Reward amount |
| PaymentType | INT | NOT NULL | PaymentType enum |
| CompensationReason | NVARCHAR(500) | NULLABLE | Why compensation triggered |
| Error fields... | Various | NULLABLE | Error tracking |

---

#### credvault_payments.dbo.RazorpayWalletTopUps

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UNIQUEIDENTIFIER | PK, Default NEWID() | Top-up unique identifier |
| UserId | UNIQUEIDENTIFIER | NOT NULL | Owner user ID |
| Amount | DECIMAL(18,2) | NOT NULL | Top-up amount |
| RazorpayOrderId | NVARCHAR(100) | UNIQUE, NOT NULL | Razorpay order ID |
| RazorpayPaymentId | NVARCHAR(100) | UNIQUE, NULLABLE | Razorpay payment ID |
| RazorpaySignature | NVARCHAR(512) | NULLABLE | Webhook signature |
| Status | INT | NOT NULL, Default 0 | RazorpayTopUpStatus enum |
| FailureReason | NVARCHAR(500) | NULLABLE | Failure description |
| CreatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Record creation time |
| UpdatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Last update time |

---

#### credvault_notifications.dbo.AuditLogs

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UNIQUEIDENTIFIER | PK, Default NEWID() | Log unique identifier |
| EntityName | NVARCHAR(100) | NOT NULL | Entity type (e.g., "User", "Card") |
| EntityId | UNIQUEIDENTIFIER | NOT NULL | Entity ID |
| Action | NVARCHAR(50) | NOT NULL | Action performed |
| UserId | UNIQUEIDENTIFIER | NOT NULL | Actor user ID |
| Changes | NVARCHAR(MAX) | NULLABLE | JSON diff of changes |
| TraceId | NVARCHAR(100) | NULLABLE | Request correlation ID |
| CreatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Log timestamp |

---

#### credvault_notifications.dbo.NotificationLogs

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UNIQUEIDENTIFIER | PK, Default NEWID() | Log unique identifier |
| UserId | UNIQUEIDENTIFIER | NOT NULL | Recipient user ID |
| Recipient | NVARCHAR(256) | NOT NULL | Email/phone recipient |
| Subject | NVARCHAR(500) | NOT NULL | Notification subject |
| Body | NVARCHAR(MAX) | NOT NULL | Notification body |
| Type | NVARCHAR(10) | NOT NULL | NotificationType enum |
| IsSuccess | BIT | NOT NULL | Delivery success flag |
| ErrorMessage | NVARCHAR(1000) | NULLABLE | Error if failed |
| TraceId | NVARCHAR(100) | NULLABLE | Request correlation ID |
| CreatedAtUtc | DATETIME2 | NOT NULL, Default GETUTCDATE() | Log timestamp |

---

## 16. EXTERNAL INTEGRATIONS

### 16.1 Google OAuth

**Purpose:** Passwordless login via Google accounts

**Flow:**
1. Frontend initiates Google Sign-In (using Google Client ID)
2. User authenticates with Google
3. Google returns IdToken to frontend
4. Frontend sends IdToken to `POST /api/v1/identity/auth/google`
5. Identity Service validates IdToken with Google's public keys
6. If user exists → login; If not → create account with Active status
7. JWT token returned

**Configuration:**
- `GoogleClientId`: Client ID from Google Cloud Console
- Token validation via Google's `https://oauth2.googleapis.com/tokeninfo` endpoint

---

### 16.2 Razorpay

**Purpose:** Wallet top-up payment processing

**Flow:**
1. User requests wallet top-up with amount
2. Payment Service creates Razorpay order via API
3. Razorpay returns order ID
4. Frontend opens Razorpay Checkout with order ID
5. User completes payment on Razorpay UI
6. Razorpay sends webhook to Payment Service
7. Payment Service verifies signature: `HMAC-SHA256(razorpayOrderId + "|" + razorpayPaymentId, razorpaySecret) == razorpaySignature`
8. If valid → update wallet balance, mark top-up complete

**Configuration:**
- `RazorpayKeyId`: Public key
- `RazorpayKeySecret`: Secret key (server-side only)
- Webhook URL: `POST /api/v1/payments/wallet/razorpay-webhook`

---

### 16.3 Gmail SMTP

**Purpose:** Email notifications

**Configuration:**
- SMTP Host: `smtp.gmail.com`
- SMTP Port: `587` (TLS)
- Authentication: App Password (Google App Password)

**Email Templates Used:**
1. Welcome email (with OTP) - on registration
2. OTP email (email verification) - on verification request
3. OTP email (password reset) - on forgot password
4. OTP email (payment) - on payment initiation
5. Card addition confirmation - on card added
6. Payment success notification - on saga completion
7. Payment failure notification - on saga compensation

---

## 17. ERROR HANDLING & RESPONSE FORMAT

### 17.1 Standard Success Response

```json
{
  "success": true,
  "data": { ... },
  "message": "Operation successful",
  "errors": []
}
```

### 17.2 Standard Error Response

```json
{
  "success": false,
  "data": null,
  "message": "Descriptive error message",
  "errors": ["Specific error 1", "Specific error 2"]
}
```

### 17.3 HTTP Status Codes

| Code | Usage |
|------|-------|
| 200 | Successful request (GET, PUT, PATCH) |
| 201 | Resource created (POST) |
| 400 | Bad request / Validation error |
| 401 | Unauthorized (missing/invalid/expired JWT) |
| 403 | Forbidden (insufficient role) |
| 404 | Resource not found |
| 409 | Conflict (duplicate resource) |
| 500 | Internal server error |

### 17.4 Exception Handling Middleware

All services use `ExceptionHandlingMiddleware` from Shared.Contracts:
- Catches all unhandled exceptions
- Logs exception with Serilog (including correlation ID)
- Returns standardized error response
- Maps known exceptions to appropriate HTTP status codes
- Hides stack traces and internal details from responses

### 17.5 Validation Errors

FluentValidation pipeline behavior runs before handlers:
- All validation errors collected and returned in `errors` array
- HTTP 400 returned with validation details
- Example:
```json
{
  "success": false,
  "data": null,
  "message": "Validation failed",
  "errors": [
    "Email is required",
    "Password must be at least 8 characters",
    "Password must contain at least one uppercase letter"
  ]
}
```

---

## 18. LOGGING & OBSERVABILITY

### 18.1 Logging Strategy

**Framework:** Serilog
**Output:** Rolling daily log files
**Format:** Structured JSON

### 18.2 Log Configuration

```json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "logs/log-.txt",
          "rollingInterval": "Day",
          "formatter": "Serilog.Formatting.Json.JsonFormatter"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithCorrelationId"]
  }
}
```

### 18.3 Log Enrichment

Each log entry includes:
- `MachineName`: Service hostname
- `CorrelationId`: Request trace ID (from HTTP header)
- `UserId`: Authenticated user ID (if available)
- `RequestId`: ASP.NET Core request ID

### 18.4 Audit Logging

Notification Service maintains `AuditLogs` table:
- Records all admin actions
- Captures entity changes as JSON diff
- Includes trace ID for request correlation
- Queryable by admin via `/api/v1/notifications/audit`

### 18.5 Notification Logging

All email attempts logged in `NotificationLogs`:
- Success/failure status
- Error message on failure
- Trace ID for correlation
- Queryable by admin via `/api/v1/notifications/logs`

---

## 19. DEPLOYMENT ARCHITECTURE

### 19.1 Docker Compose Services

```yaml
services:
  identity-service:       # Port 5001
  card-service:           # Port 5002
  billing-service:        # Port 5003
  payment-service:        # Port 5004
  notification-service:   # Port 5005
  gateway:                # Port 5006 (exposed to host)
  rabbitmq:               # Port 5672, Management: 15672
  sqlserver:              # Port 1433
```

### 19.2 Network Topology

```
┌─────────────────────────────────────────────────────┐
│                    Docker Network                    │
│                                                     │
│  ┌──────────┐                                       │
│  │ Gateway  │:5006 ──> Exposed to host              │
│  └────┬─────┘                                       │
│       │ Internal HTTP                               │
│  ┌────┴──────────────────────┐                      │
│  │  Microservices            │                      │
│  │  :5001 :5002 :5003        │                      │
│  │  :5004 :5005              │                      │
│  └────┬──────────────────────┘                      │
│       │                                              │
│  ┌────┴──────────┐  ┌────────────┐                  │
│  │   RabbitMQ    │  │ SQL Server │                  │
│  │  :5672/:15672 │  │   :1433    │                  │
│  └───────────────┘  └────────────┘                  │
│                                                     │
└─────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────┐     ┌──────────────┐
│ Angular Frontend    │     │ External APIs│
│ (Dev: :4200         │     │ Google,      │
│  Prod: Served by    │     │ Razorpay,    │
│  Express SSR)       │     │ Gmail SMTP)  │
└─────────────────────┘     └──────────────┘
```

### 19.3 Database Initialization

- 5 databases created on SQL Server startup:
  - `credvault_identity`
  - `credvault_cards`
  - `credvault_billing`
  - `credvault_payments`
  - `credvault_notifications`
- Each service runs EF Core migrations on startup (`Database.Migrate()`)
- Seed data: Card issuers (Visa, Mastercard, Rupay, Amex), default reward tiers

### 19.4 Environment Variables

| Variable | Service | Description |
|----------|---------|-------------|
| `ConnectionStrings__Default` | All services | SQL Server connection string |
| `Jwt__Key` | All services | JWT signing key |
| `RabbitMQ__Host` | All services | RabbitMQ hostname |
| `RabbitMQ__Port` | All services | RabbitMQ port |
| `RabbitMQ__Username` | All services | RabbitMQ username |
| `RabbitMQ__Password` | All services | RabbitMQ password |
| `Google__ClientId` | Identity Service | Google OAuth client ID |
| `Razorpay__KeyId` | Payment Service | Razorpay public key |
| `Razorpay__KeySecret` | Payment Service | Razorpay secret key |
| `Smtp__Host` | Notification Service | SMTP host |
| `Smtp__Port` | Notification Service | SMTP port |
| `Smtp__Username` | Notification Service | SMTP username |
| `Smtp__Password` | Notification Service | SMTP app password |
| `Gateway__BaseUrl` | All services | Gateway base URL |

---

## 20. TESTING STRATEGY

### 20.1 Backend Testing (.NET)

Each service has a dedicated `*.Tests` project:
- **Unit Tests**: Individual handler/command/test logic
- **Integration Tests**: API endpoint tests with in-memory database
- **Test Framework**: xUnit / NUnit
- **Mocking**: Moq for external dependencies

### 20.2 Frontend Testing (Angular)

- **Unit Tests**: Component and service tests
- **Test Framework**: Vitest
- **Coverage**: Services, guards, interceptors, pipes

### 20.3 Test Categories

| Category | Scope | Tools |
|----------|-------|-------|
| Unit | Individual functions/handlers | xUnit, Moq |
| Integration | API endpoints, DB operations | WebApplicationFactory |
| Component | Angular components | Vitest, Angular Testing |
| E2E | Full user flows | (Not implemented) |

---

## 21. BUSINESS RULES

### 21.1 User Rules
- Email must be unique across all users
- Password minimum: 8 characters, 1 uppercase, 1 lowercase, 1 digit
- OTP expires after 10 minutes (5 minutes for payment OTP)
- Suspended users cannot login or perform any action
- SSO users have null PasswordHash

### 21.2 Card Rules
- Card number is encrypted before storage (AES)
- Only last 4 digits are stored in plain text
- Soft delete (IsDeleted flag) instead of physical deletion
- Deleted cards are excluded from all queries
- A user can have multiple cards
- One card can be marked as default

### 21.3 Billing Rules
- Minimum due is calculated as a percentage of total bill amount
- Bills transition to "Overdue" if not paid by due date
- Partial payments are allowed (status → PartiallyPaid)
- Statements aggregate all transactions within a billing period
- Statements include: opening balance, purchases, payments, refunds, charges

### 21.4 Rewards Rules
- Reward rate is determined by reward tier (network + issuer specific)
- Rewards earned on payment completion
- Rewards can be redeemed during payment (reduces payment amount)
- Reward redemption is reversible (saga compensation)
- Reward tiers have effective date ranges
- Each user has exactly one reward account

### 21.5 Payment Rules
- Payments require OTP verification (2FA)
- Payment can be via Wallet or Card
- Wallet balance must be sufficient for wallet payments
- Payment OTP expires after 5 minutes
- Expired payments are cleaned up by background job
- All payments go through saga orchestration

### 21.6 Wallet Rules
- Each user has exactly one wallet
- Wallet is auto-created on first use
- Wallet balance cannot go negative
- Wallet transactions are immutable (only new transactions added)
- Razorpay top-ups are verified via signature validation
- Duplicate webhooks are handled idempotently

### 21.7 Saga Rules
- Saga is idempotent (same correlation ID = same saga)
- Compensation is triggered on any step failure
- Compensation executes in reverse order
- Saga state is persisted for recovery
- Timeout on any step triggers compensation

---

## 22. FRONTEND ARCHITECTURE

### 22.1 Project Structure

```
client/
├── package.json                    # Angular 21.2, Tailwind, Chart.js, RxJS
├── angular.json                    # Build config, SSR support
├── tsconfig.json                   # Strict TypeScript config
├── tailwind.config.js              # Tailwind CSS config
├── .env / .env.example             # Environment variables
├── scripts/generate-runtime-env.js # Runtime env generation for Docker
└── src/
    ├── main.ts                     # Browser bootstrap
    ├── main.server.ts              # SSR bootstrap
    ├── server.ts                   # Express SSR server
    ├── index.html                  # HTML template
    ├── styles.css                  # Global styles (Tailwind imports)
    ├── environments/
    │   ├── environment.ts          # Dev config
    │   ├── environment.prod.ts     # Prod config
    │   └── environment.generated.ts # Docker runtime config
    └── app/
        ├── app.ts                  # Root component (Navbar + Footer + RouterOutlet)
        ├── app.config.ts           # App providers (Router, HttpClient, interceptors)
        ├── app.routes.ts           # All route definitions
        ├── core/
        │   ├── services/
        │   │   ├── auth.service.ts          # Auth operations, JWT management
        │   │   ├── billing.service.ts       # Bill operations
        │   │   ├── dashboard.service.ts     # Dashboard data
        │   │   ├── payment.service.ts       # Payment + OTP operations
        │   │   ├── rewards.service.ts       # Rewards operations
        │   │   ├── wallet.service.ts        # Wallet + Razorpay operations
        │   │   └── admin.service.ts         # Admin operations
        │   ├── guards/
        │   │   ├── auth.guard.ts            # Requires authenticated
        │   │   ├── guest.guard.ts           # Requires unauthenticated
        │   │   └── admin.guard.ts           # Requires admin role
        │   ├── interceptors/
        │   │   └── auth.interceptor.ts      # Attaches Bearer token, handles 401
        │   ├── models/
        │   │   ├── auth.models.ts           # Auth-related interfaces
        │   │   └── card.models.ts           # Card-related interfaces
        │   ├── components/                  # Shared UI components (Navbar, Footer)
        │   └── utils/                       # Utility functions
        ├── features/
        │   ├── auth/                        # Login, Register, Verify, Forgot/Reset
        │   ├── dashboard/                   # User dashboard
        │   ├── cards/                       # Card management
        │   ├── bills/                       # Bill list, wallet activity
        │   ├── statements/                  # Statements list, detail, analytics
        │   ├── rewards/                     # Rewards display
        │   ├── profile/                     # User profile management
        │   └── admin/                       # Admin panel with sub-routes
        └── shared/
            └── pipes/
                └── ist-date.pipe.ts         # IST date formatting pipe
```

### 22.2 Frontend Route Structure

| Route | Component | Guard | Description |
|-------|-----------|-------|-------------|
| `/` | Dashboard | authGuard | User dashboard |
| `/login` | Login | guestGuard | Login page |
| `/register` | Register | guestGuard | Registration page |
| `/verify` | VerifyEmail | guestGuard | Email verification |
| `/forgot-password` | ForgotPassword | guestGuard | Forgot password |
| `/reset-password` | ResetPassword | guestGuard | Reset password |
| `/cards` | CardDetails | authGuard | Card management |
| `/bills` | BillsList | authGuard | Bill list |
| `/statements` | StatementsList | authGuard | Statements |
| `/statements/:id` | StatementDetail | authGuard | Statement detail |
| `/rewards` | Rewards | authGuard | Rewards page |
| `/profile` | Profile | authGuard | User profile |
| `/admin` | AdminLayout | adminGuard | Admin panel |
| `/admin/users` | AdminUsers | adminGuard | User management |
| `/admin/cards` | AdminCards | adminGuard | Card management |
| `/admin/bills` | AdminBills | adminGuard | Bill management |
| `/admin/rewards` | AdminRewards | adminGuard | Reward tier management |
| `/admin/logs` | AdminLogs | adminGuard | Notification/audit logs |

### 22.3 Frontend Environment Configuration

```typescript
// environment.ts (dev)
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5006',  // Gateway
  googleClientId: '<google-client-id>',
  razorpayKeyId: '<razorpay-key-id>'
};
```

### 22.4 Auth Interceptor Behavior

```typescript
// On every HTTP request:
// 1. Get token from sessionStorage ('cv_token')
// 2. If token exists: Add 'Authorization: Bearer <token>' header
// 3. On 401 response: Clear sessionStorage, redirect to /login
```

---

## 23. SHARED CONTRACTS LIBRARY

### 23.1 Purpose
Shared.Contracts is a .NET class library referenced by all microservices to ensure consistency.

### 23.2 Contents

#### Controllers
- `BaseApiController`: Base class for all controllers
  - Standardized response methods: `Success()`, `Created()`, `BadRequest()`, `NotFound()`
  - All responses wrapped in `ApiResponse<T>` envelope

#### DTOs
- `OperationResult<T>`: Generic result wrapper with success/failure state
- Identity DTOs: Register, Login, Profile, etc.
- Card DTOs: Card request/response
- Payment DTOs: Payment request/response

#### Enums
- `CardNetwork`: Visa, Mastercard, Rupay, Amex
- All enums shared across services

#### Events
- All event interfaces (`IUserRegistered`, `ICardAdded`, etc.)
- Saga event interfaces
- Shared event contracts ensure publisher/consumer compatibility

#### Extensions
- `ServiceCollectionExtensions.AddStandardAuth()`: JWT configuration
- `ServiceCollectionExtensions.AddStandardSwagger()`: Swagger setup
- `ServiceCollectionExtensions.AddStandardMessaging()`: MassTransit setup

#### Middleware
- `ExceptionHandlingMiddleware`: Global exception handler
  - Catches all unhandled exceptions
  - Logs with Serilog
  - Returns standardized error response

#### Models
- `ApiResponse<T>`: Standard API response envelope
  - `Success`, `Data`, `Message`, `Errors`

---

## 24. DIAGRAM GENERATION INSTRUCTIONS

### 24.1 What to Generate

When given this document, generate ALL of the following:

#### 1. High-Level Architecture Diagram (HLD)
- Show all components: Frontend, Gateway, 5 Services, 5 Databases, RabbitMQ, External APIs
- Show communication flows (HTTP sync, AMQP async)
- Show port numbers
- Use color coding: Client tier, Gateway tier, Service tier, Data tier, External tier

#### 2. Low-Level Design Diagrams (LLD)
- For EACH service, show the Clean Architecture layers
- Show entities within Domain layer
- Show commands/queries within Application layer
- Show DbContext/repositories within Infrastructure layer
- Show controllers within API layer
- Show cross-layer dependencies

#### 3. Entity Relationship Diagrams (ERD)
- For EACH database, show all tables with columns
- Show relationships (1:N, 1:1)
- Mark primary keys, foreign keys, unique constraints, indexes
- Show data types
- Note: NO cross-database relationships

#### 4. Sequence Diagrams
- User Registration (Section 7.1)
- User Login (email/password)
- Google OAuth Login (Section 7.5)
- Email Verification
- Card Addition (Section 7.4)
- Bill Payment with Saga (Section 7.2)
- Saga Compensation/Failure (Section 7.3)
- Wallet Top-Up via Razorpay (Section 7.6)
- Password Reset Flow
- Reward Redemption Flow

#### 5. State Machine Diagrams
- User Status lifecycle (Section 9.1)
- Bill Status lifecycle (Section 9.2)
- Payment Status lifecycle (Section 9.3)
- Payment Saga detailed state machine (Section 9.4 / 14.3)

#### 6. Use Case Diagrams
- Show all actors (Guest, Registered User, Admin, System, External)
- Show all use cases (UC-001 through UC-012)
- Show relationships (include, extend)

#### 7. API Reference Diagram
- Show all 58 endpoints organized by service
- Show HTTP method, path, auth requirement
- Group by resource

#### 8. Event Flow Diagram
- Show RabbitMQ as central hub
- Show which services publish which events
- Show which services consume which events
- Separate normal events from saga events

#### 9. Deployment Diagram
- Show Docker Compose structure
- Show container relationships
- Show exposed vs internal ports
- Show volume mounts for databases

#### 10. Data Flow Diagrams (DFD)
- Level 0: System context
- Level 1: Major processes (Auth, Card Mgmt, Billing, Payment, Notification)
- Level 2: Detailed flows for payment saga

### 24.2 Visual Conventions

| Element | Shape | Color |
|---------|-------|-------|
| Client/Frontend | Rectangle | Blue |
| API Gateway | Hexagon | Purple |
| Microservice | Rounded Rectangle | Green |
| Database | Cylinder | Orange |
| Message Broker | Diamond | Red |
| External Service | Cloud | Gray |
| HTTP/Sync Flow | Solid arrow | Black |
| AMQP/Async Flow | Dashed arrow | Blue |
| Database Connection | Solid arrow | Orange |

### 24.3 Diagram Quality Requirements

- Professional, enterprise-grade appearance
- Clear hierarchy and grouping
- Legible text at standard zoom
- Consistent color scheme
- Include legend/key
- Label all arrows with protocol/method
- Show port numbers where applicable
- Use consistent font sizes
- Avoid overlapping lines
- Group related components with bounding boxes

---

## APPENDIX A: QUICK REFERENCE CARD

| Service | Port | Database | Key Responsibility |
|---------|------|----------|-------------------|
| Identity | 5001 | credvault_identity | Auth, Users, JWT, OTP |
| Card | 5002 | credvault_cards | Cards, Transactions, Issuers |
| Billing | 5003 | credvault_billing | Bills, Statements, Rewards |
| Payment | 5004 | credvault_payments | Payments, Wallet, Saga |
| Notification | 5005 | credvault_notifications | Email, Audit, Logs |
| Gateway | 5006 | N/A | Request routing |
| RabbitMQ | 5672 | N/A | Async messaging |
| SQL Server | 1433 | 5 DBs | Data persistence |

## APPENDIX B: KEY DESIGN DECISIONS

1. **Database-per-Service**: Each service owns its database; no shared databases
2. **No Cross-Service FK Constraints**: Eventual consistency via events
3. **Saga for Distributed Transactions**: Payment flow uses saga instead of 2PC
4. **API Gateway Pattern**: Single entry point; services not directly accessible
5. **Clean Architecture**: Consistent layering across all services
6. **CQRS**: MediatR separates reads from writes
7. **Shared Contracts**: Common code shared via class library
8. **Event-Driven Notifications**: All notifications via RabbitMQ events
9. **Soft Deletes**: Cards use IsDeleted flag
10. **OTP for All Sensitive Operations**: Email verification, password reset, payments

---

*END OF CREDVAULT COMPLETE SYSTEM SPECIFICATION*

*This document contains all information required to generate: HLD, LLD, ER diagrams, sequence diagrams, state machines, use case diagrams, API references, event flow diagrams, deployment diagrams, and data flow diagrams.*
