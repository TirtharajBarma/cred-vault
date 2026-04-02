# CredVault — High-Level System Design (HLD)

> Version: 1.0 | Date: April 3, 2026

---

## 1. System Overview

CredVault is a premium credit card management platform that allows users to manage credit cards, pay bills, track rewards, and view statements. It is built on a microservices architecture with an Angular frontend and a .NET 10 backend.

---

## 2. Architecture Style

- Pattern: Microservices
- Communication: REST (synchronous) + RabbitMQ (asynchronous event-driven)
- Orchestration: SAGA pattern (MassTransit state machine) for distributed transactions
- Frontend: Angular 21 SPA with SSR, communicates exclusively through the API Gateway

---

## 3. System Context Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                          End Users                                  │
│              (Browser / Mobile Browser)                             │
└──────────────────────────────┬──────────────────────────────────────┘
                               │ HTTPS
                               ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    Angular Frontend (SSR)                           │
│                     client/ — Port 4200                             │
└──────────────────────────────┬──────────────────────────────────────┘
                               │ HTTP REST
                               ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    Ocelot API Gateway                               │
│                        Port 5006                                    │
└──────┬──────────┬──────────┬──────────┬──────────┬─────────────────┘
       │          │          │          │          │
       ▼          ▼          ▼          ▼          ▼
  Identity     Card      Billing    Payment  Notification
  Service     Service    Service    Service    Service
  :5001        :5002      :5003      :5004      :5005
```

---

## 4. Services Overview

| Service | Port | Responsibility | Database |
|---|---|---|---|
| API Gateway | 5006 | Route, proxy, CORS | — |
| Identity Service | 5001 | Auth, users, OTP, JWT | credvault_identity |
| Card Service | 5002 | Cards, issuers, transactions, violations | credvault_cards |
| Billing Service | 5003 | Bills, statements, rewards | credvault_billing |
| Payment Service | 5004 | Payment orchestration, SAGA, OTP | credvault_payments |
| Notification Service | 5005 | Email notifications, audit logs | credvault_notifications |

Each service owns its own SQL Server database — no shared database.

---

## 5. Component Architecture

### 5.1 API Gateway (Ocelot)

- Single entry point for all client traffic
- Routes requests to downstream services based on path prefix
- Handles CORS policy
- Does not perform authentication (delegates to downstream services)

```
/api/v1/identity/*      → Identity Service  (5001)
/api/v1/cards/*         → Card Service      (5002)
/api/v1/issuers/*       → Card Service      (5002)
/api/v1/billing/*       → Billing Service   (5003)
/api/v1/payments/*      → Payment Service   (5004)
/api/v1/notifications/* → Notification Svc  (5005)
```

### 5.2 Identity Service

Responsibilities:
- User registration with email OTP verification
- JWT-based login (access token, 30-minute expiry)
- Password reset via OTP
- User profile management
- Admin: user status and role management

Key events published:
- `IUserRegistered` → triggers welcome email
- `IUserOtpGenerated` → triggers OTP email

### 5.3 Card Service

Responsibilities:
- Credit card CRUD (soft delete)
- Card issuer management (Visa / Mastercard)
- Card transaction recording (Purchase, Payment, Refund)
- Card health score calculation
- Violation and strike tracking (blocks card at 3 strikes)

Key events consumed:
- `IBillOverdueDetected` → applies strike
- `IBillUpdateSucceeded` → clears strikes
- `ICardDeductionRequested` → deducts outstanding balance (SAGA step)
- `IPaymentReversed` → refunds card balance

### 5.4 Billing Service

Responsibilities:
- Bill generation (admin-triggered)
- Bill status management (Pending → Paid / Overdue)
- Statement auto-generation on payment
- Reward tier configuration
- Reward account and points management

Key events consumed:
- `IBillUpdateRequested` → marks bill paid (SAGA step)
- `IRevertBillUpdateRequested` → reverts bill payment (compensation)

Key events published:
- `IBillGenerated` → triggers bill notification
- `IBillOverdueDetected` → triggers card strike

### 5.5 Payment Service

Responsibilities:
- Payment initiation with OTP generation
- SAGA orchestration for distributed payment flow
- Risk scoring (calculated, not yet enforced)
- Payment reversal

SAGA States:
```
Initial
  → AwaitingOtpVerification
    → AwaitingPaymentConfirmation
      → AwaitingBillUpdate
        → AwaitingCardDeduction
          → Completed ✓
          → Compensating → Compensated (on failure)
```

### 5.6 Notification Service

Responsibilities:
- Consumes all domain events from `notification-domain-event` queue
- Sends transactional emails via Gmail SMTP
- Stores notification logs and audit logs

---

## 6. Data Architecture

Each service has an isolated database. Cross-service data references (e.g., `CardId` in Billing) are stored as plain GUIDs without foreign key constraints — consistency is maintained through events.

### Key Entities per Service

```
Identity:   identity_users
Card:       CreditCards, CardIssuers, CardTransactions, CardViolations
Billing:    Bills, Statements, StatementTransactions,
            RewardAccounts, RewardTiers, RewardTransactions
Payment:    Payments, Transactions, PaymentOrchestrationSagas,
            RiskScores, FraudAlerts
Notification: NotificationLogs, AuditLogs
```

---

## 7. Asynchronous Messaging Architecture

Message broker: RabbitMQ (MassTransit)

### Queue Map

| Queue | Consumer | Trigger |
|---|---|---|
| `billing-payment-completed` | BillingService | Payment SAGA completed |
| `billing-payment-reversed` | BillingService | Payment reversed |
| `card-payment-completed` | CardService | Payment completed |
| `notification-domain-event` | NotificationService | All domain events |
| `notification-domain-event_error` | DLQ | Failed after 3 retries |
| `payment-fraud-detected` | (future) | Fraud detected |
| `payment-payment-completed` | PaymentService | Internal SAGA |
| `payment-payment-failed` | PaymentService | Internal SAGA |

### Retry Policy

- 3 automatic retry attempts (immediate)
- On 3rd failure → message moved to Dead Letter Queue (DLQ)
- DLQ: `notification-domain-event_error`

---

## 8. Payment Flow (End-to-End)

```
User
 │
 ├─ POST /api/v1/payments/initiate
 │       │
 │       ▼
 │   PaymentService
 │   ├─ Validates bill (calls BillingService)
 │   ├─ Generates OTP
 │   ├─ Creates Payment record (status: Initiated)
 │   └─ Publishes IStartPaymentOrchestration
 │
 ├─ POST /api/v1/payments/{id}/verify-otp
 │       │
 │       ▼
 │   PaymentService publishes IOtpVerified
 │       │
 │       ▼
 │   SAGA: AwaitingPaymentConfirmation
 │   → IPaymentProcessRequested → PaymentService marks Processing
 │   → IPaymentProcessSucceeded
 │       │
 │       ▼
 │   SAGA: AwaitingBillUpdate
 │   → IBillUpdateRequested → BillingService marks Bill Paid
 │   → IBillUpdateSucceeded
 │       │
 │       ▼
 │   SAGA: AwaitingCardDeduction
 │   → ICardDeductionRequested → CardService deducts balance
 │   → ICardDeductionSucceeded
 │       │
 │       ▼
 │   SAGA: Completed
 │   → IPaymentCompleted
 │       ├─ BillingService: credits reward points
 │       ├─ CardService: clears strikes
 │       └─ NotificationService: sends confirmation email
```

---

## 9. Authentication & Authorization

- Mechanism: JWT Bearer tokens
- Issuer: `IdentityService`
- Audience: `CredVaultClients`
- Token expiry: 30 minutes
- Roles: `user`, `admin`
- Auth enforced at each downstream service (not at gateway)

Role-based access:
- `user` — own resources only
- `admin` — all users, card config, bill generation, reward tiers, logs

---

## 10. Frontend Architecture

- Framework: Angular 21 (standalone components)
- Rendering: SSR enabled via `@angular/ssr`
- State: Angular Signals
- Styling: Tailwind CSS v4
- HTTP: `HttpClient` with JWT interceptor
- Routing: Lazy-loaded feature modules

### Feature Modules

| Module | Route | Access |
|---|---|---|
| Auth | `/login`, `/register`, `/verify-email`, etc. | Public |
| Dashboard | `/dashboard` | Authenticated |
| Cards | `/cards/:id` | Authenticated |
| Bills | `/bills` | Authenticated |
| Payments | `/payments` | Authenticated |
| Statements | `/statements` | Authenticated |
| Rewards | `/rewards` | Authenticated |
| Notifications | `/notifications` | Authenticated |
| Profile | `/profile` | Authenticated |
| Admin | `/admin/*` | Admin role |

---

## 11. Infrastructure & Deployment (Local Dev)

| Component | Host | Port |
|---|---|---|
| Angular Frontend | localhost | 4200 |
| API Gateway | localhost | 5006 |
| Identity Service | localhost | 5001 |
| Card Service | localhost | 5002 |
| Billing Service | localhost | 5003 |
| Payment Service | localhost | 5004 |
| Notification Service | localhost | 5005 |
| SQL Server | localhost | 1434 |
| RabbitMQ | localhost | 5672 |
| RabbitMQ Management UI | localhost | 15672 |

---

## 12. Key Design Decisions

| Decision | Rationale |
|---|---|
| Microservices over monolith | Independent deployability, domain isolation |
| SAGA pattern for payments | Distributed transaction consistency without 2PC |
| Event-driven notifications | Decouples notification logic from business logic |
| Each service owns its DB | Prevents tight coupling, enables independent scaling |
| Ocelot API Gateway | Single entry point, simplifies client routing |
| MassTransit over raw RabbitMQ | Retry, DLQ, SAGA state machine out of the box |

---

## 13. Known Limitations & Future Considerations

- No real payment gateway integration (payments are simulated)
- Risk scoring is calculated but not enforced in the SAGA flow
- No circuit breakers for inter-service HTTP calls
- Gmail SMTP used for email (not production-grade)
- No horizontal scaling configuration (single instance per service)
- OAuth / SSO login is UI-only (not implemented in backend)
- Overdue bill detection is manually triggered by admin (not scheduled)
