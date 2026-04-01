# Messaging Infrastructure Analysis - .NET Microservices

**Analysis Date:** 2026-03-31

---

## Executive Summary

This analysis covers the RabbitMQ messaging infrastructure across 5 microservices:
- **Identity Service** (publisher only)
- **Card Service** (consumer)
- **Billing Service** (consumer)
- **Payment Service** (publisher + consumer + Saga)
- **Notification Service** (consumer)

---

## 1. Consumers That Receive Messages But No One Publishes Them

### Finding 1: `IOTPVerified` - Saga-Breaking Issue

| Item | Details |
|------|---------|
| **Message** | `IOTPVerified` |
| **Expected Consumer** | MassTransit Saga (`PaymentSaga`) |
| **Publisher** | `VerifyPaymentOtpCommand.cs` (line 21) |
| **Problem** | The saga CORRELATES on this event (line 127), but it may not be properly wired to receive it |

**File:** `/Users/tirtharaj/Desktop/Desktop/self-learning/project/src/server/services/payment-service/PaymentService.Application/Sagas/PaymentSaga.cs`
- Line 127: `When(OTPVerified)` - saga waits for this event
- Line 18: `Event<IOTPVerified> OTPVerified { get; private set; }` - defined but needs proper subscription

**Recommendation:**
1. Verify the `VerifyPaymentOtpCommand` publishes to the correct queue that the saga subscribes to
2. Ensure correlation IDs match between publish and consume
3. Add logging in the saga to confirm receipt of `IOTPVerified`

---

### Finding 2: `IBillGenerated` - No Active Consumer Found

| Item | Details |
|------|---------|
| **Message** | `IBillGenerated` |
| **Publisher** | `GenerateAdminBillCommand.cs` (line 73) |
| **Declared Consumer** | `DomainEventConsumer.cs` (line 38-43) in NotificationService |
| **Status** | Only notification consumer exists |

**File:** `/Users/tirtharaj/Desktop/Desktop/self-learning/project/src/server/shared.contracts/Shared.Contracts/Events/Billing/BillingEvents.cs`

**Current Flow:**
```csharp
// Published by BillingService
await publishEndpoint.Publish<IBillGenerated>(new { BillId = bill.Id, ... });

// Consumed only by NotificationService for sending email
public async Task Consume(ConsumeContext<IBillGenerated> context)
{
    await mediator.Send(new ProcessNotificationCommand("BillGenerated", ...));
}
```

**Recommendation:**
- This is likely intentional - billing system only needs to notify users. No other service acts on bill generation.
- **Status:** Not an issue - working as designed.

---

## 2. Duplicate Consumers Doing Same Work

### Finding 3: Four `IUserDeleted` Consumers - Justified Duplication

| Service | File | Action |
|---------|------|--------|
| Card Service | `CardService.API/Messaging/UserDeletedConsumer.cs` | Soft-delete cards |
| Billing Service | `BillingService.API/Messaging/UserDeletedConsumer.cs` | Cancel bills, remove rewards |
| Payment Service | `PaymentService.Infrastructure/Messaging/Consumers/UserDeletedConsumer.cs` | Cancel initiated payments |
| Notification Service | `NotificationService.Application/Consumers/DomainEventConsumer.cs` | Notify admin |

**Analysis:**
- Each service handles its OWN domain data when a user is deleted
- This is **correct CQRS pattern** - each bounded context handles its own cleanup
- No duplication of business logic

**Recommendation:** No changes needed - this is proper distributed architecture.

---

### Finding 4: Three `IPaymentCompleted` Consumers - Proper Distribution

| Service | File | Action |
|---------|------|--------|
| Payment Service | `PaymentConsumers.cs` (lines 10-63) | Update payment status to Completed |
| Card Service | `PaymentCompletedConsumer.cs` | Update card balance |
| Billing Service | `PaymentCompletedConsumer.cs` | Mark bill as paid |
| Notification Service | `DomainEventConsumer.cs` | Send payment confirmation |

**Analysis:**
- Each consumer handles different domain logic
- **Potential Issue:** PaymentService publishes `IPaymentCompleted` (line 121 in InitiatePaymentCommand), but also has a consumer for it.

**Files:**
- Publisher: `/Users/tirtharaj/Desktop/Desktop/self-learning/project/src/server/services/payment-service/PaymentService.Application/Commands/Payments/InitiatePaymentCommand.cs` (line 119)
- Consumer: `/Users/tirtharaj/Desktop/Desktop/self-learning/project/src/server/services/payment-service/PaymentService.Infrastructure/Messaging/Consumers/PaymentConsumers.cs` (line 10)

**Recommendation:**
- The PaymentService consumer might be redundant if the Saga handles state transitions internally
- Consider whether `PaymentCompletedConsumer` in PaymentService adds value beyond what the Saga does
- Check if lines 26-30 (idempotency check) are necessary given Saga state machine

---

### Finding 5: `IPaymentFailed` Consumer Gap in Billing

| Message | Card Service | Billing Service | Payment Service | Notification Service |
|---------|--------------|-----------------|-----------------|---------------------|
| IPaymentFailed | ❌ | ❌ | ✅ | ✅ |

**Finding:**
- BillingService DOES NOT consume `IPaymentFailed`
- If a payment fails, the bill stays "paid" in billing system
- Only `PaymentReversed` triggers bill reversal (line 41-71 in `PaymentCompletedConsumer.cs`)

**File:** `/Users/tirtharaj/Desktop/Desktop/self-learning/project/src/server/services/billing-service/BillingService.API/Messaging/PaymentCompletedConsumer.cs`

**Recommendation:**
- Add `IPaymentFailed` consumer in BillingService to mark bill as FAILED (not reversed)
- Or clarify that failed payments are handled via a different workflow

---

## 3. Over-Engineered Saga Patterns

### Finding 6: PaymentSaga Self-Publishes `IPaymentCompleted`

**Issue:** The saga publishes `IPaymentCompleted` internally, then a consumer also listens for it.

**File:** `/Users/tirtharaj/Desktop/Desktop/self-learning/project/src/server/services/payment-service/PaymentService.Application/Sagas/PaymentSaga.cs`

```csharp
// Saga publishes at lines 110 and 130
.PublishAsync(ctx => ctx.Init<IPaymentCompleted>(new { ... }));

// But there's also a consumer at PaymentConsumers.cs line 10
public class PaymentCompletedConsumer(...) : IConsumer<IPaymentCompleted>
```

**Problems:**
1. **Circular dependency** - Service publishes and consumes same message
2. **Confusion** - Is the consumer handling external events or internal saga events?
3. **Double processing** - Both Saga and Consumer may process the same event

**Recommendation:**
1. **Option A (Preferred):** Remove `PaymentCompletedConsumer` from PaymentService - let Saga manage state
2. **Option B:** Rename to clarify it's for external correlation only

---

### Finding 7: Saga Stores OTP - Security Concern

**File:** `/Users/tirtharaj/Desktop/Desktop/self-learning/project/src/server/services/payment-service/PaymentService.Domain/Entities/PaymentSagaState.cs`

```csharp
// Line 23-24
public string? OtpCode { get; set; }
public DateTime? OtpExpiresAtUtc { get; set; }
```

**Issue:**
- OTP is stored in Saga state (persisted to database)
- This means OTP survives service restarts
- Risk: If database is compromised, OTPs are exposed

**Recommendation:**
- Store only hashed OTP, or use Redis with TTL instead of database saga state
- Current implementation stores plain OTP - consider hashing before storage

---

### Finding 8: Saga State Machine Has Unused States

**File:** `/Users/tirtharaj/Desktop/Desktop/self-learning/project/src/server/services/payment-service/PaymentService.Application/Sagas/PaymentSaga.cs`

States defined:
- `Initial` (line 10)
- `Initiated` (line 11)
- `RiskCheckPassed` (line 12)
- `Processing` (line 13)
- `Completed` (line 14)
- `Failed` (line 15)

**Analysis:**
- State transitions look correct
- However, the saga doesn't handle compensation/rollback explicitly
- `PaymentReversed` is published by `ReversePaymentCommand` but Saga doesn't participate in reversal

**Recommendation:**
- Consider if saga should also handle `IPaymentReversed` for consistency
- Current: Reversal is handled by separate command, not saga

---

## 4. Unused Message Types

### Finding 9: `IOTPVerified` Consumer Gap

| Message | Published By | Consumed By |
|---------|--------------|-------------|
| `IOTPVerified` | VerifyPaymentOtpCommand.cs | **NONE** |

**File:** `/Users/tirtharaj/Desktop/Desktop/self-learning/project/src/server/shared.contracts/Shared.Contracts/Events/Payment/PaymentEvents.cs` (lines 62-67)

```csharp
public interface IOTPVerified
{
    Guid PaymentId { get; }
    string OtpCode { get; }
    DateTime VerifiedAt { get; }
}
```

**Analysis:**
- The Saga CORRELATES on this event but may not be receiving it properly
- No explicit consumer registration found

**Recommendation:**
1. Verify MassTransit saga subscription is correct
2. Add explicit consumer logging to debug why saga doesn't receive OTP verified events
3. Consider if this should be a separate consumer instead of saga correlation

---

### Finding 10: `IFraudDetected` - Only One Consumer

| Message | Published By | Consumed By |
|---------|--------------|-------------|
| `IFraudDetected` | PaymentSaga (line 65) | PaymentService (FraudDetectedConsumer) + NotificationService |

**Files:**
- Publisher: `PaymentSaga.cs` line 65 (inside saga when risk score >= 75)
- Consumer 1: `PaymentConsumers.cs` lines 103-146 (creates FraudAlert + RiskScore)
- Consumer 2: `DomainEventConsumer.cs` lines 66-71 (notifies security team)

**Status:** Working correctly - both services need to know about fraud.

---

## 5. Program.cs Message Registration Analysis

### Card Service (`Program.cs` lines 45-67)

```csharp
x.AddConsumer<PaymentCompletedConsumer>();  // ✅
x.AddConsumer<UserDeletedConsumer>();        // ✅

cfg.ReceiveEndpoint("card-domain-event", e => { ... });
```

**Status:** Correct - only needs PaymentCompleted and UserDeleted

---

### Billing Service (`Program.cs` lines 48-72)

```csharp
x.AddConsumer<PaymentCompletedConsumer>();   // ✅
x.AddConsumer<PaymentReversedConsumer>();    // ✅  
x.AddConsumer<UserDeletedConsumer>();        // ✅
```

**Issue:** Missing `IPaymentFailed` consumer
**Recommendation:** Add consumer or document why not needed

---

### Payment Service (`Program.cs` lines 57-83)

```csharp
x.AddConsumer<PaymentCompletedConsumer>();   // ⚠️ May be redundant with Saga
x.AddConsumer<PaymentFailedConsumer>();      // ✅
x.AddConsumer<FraudDetectedConsumer>();      // ✅
x.AddConsumer<UserDeletedConsumer>();        // ✅
```

**Issue:** `PaymentCompletedConsumer` may conflict with Saga
**Recommendation:** Review if consumer needed given Saga publishes IPaymentCompleted

---

### Notification Service (`Program.cs` lines 43-63)

```csharp
x.AddConsumer<DomainEventConsumer>();        // Handles 9 event types
```

**Status:** All 9 event types have publishers:
- IUserRegistered, IUserOtpGenerated, IUserDeleted ✅
- ICardAdded ✅
- IBillGenerated ✅
- IPaymentOtpGenerated, IPaymentCompleted, IPaymentFailed ✅
- IFraudDetected ✅

---

### Finding 11: **CRITICAL - PaymentSaga Not Registered in Program.cs**

**Issue:** The `PaymentSaga` state machine is defined but **never registered** with MassTransit.

**File:** `/Users/tirtharaj/Desktop/Desktop/self-learning/project/src/server/services/payment-service/PaymentService.API/Program.cs`

```csharp
// Lines 57-83 - Missing Saga registration!
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.AddConsumer<PaymentCompletedConsumer>();   // Only consumers registered
    x.AddConsumer<PaymentFailedConsumer>();
    x.AddConsumer<FraudDetectedConsumer>();
    x.AddConsumer<UserDeletedConsumer>();
    // ❌ x.AddSaga<PaymentSaga>() is MISSING!
});
```

**Impact:**
1. The entire PaymentSaga orchestration is **non-functional**
2. `IOTPVerified` events are not being processed (saga doesn't exist to receive them)
3. Payment flow relies only on consumers, not saga
4. Risk scoring logic in saga (lines 57-122) never executes

**Recommendation:**
- **Add saga registration:**
```csharp
x.AddSaga<PaymentSaga>();
```
- Configure saga state repository (database or Redis)
- Add endpoint configuration for saga:
```csharp
cfg.ConfigureSaga<PaymentSaga>(ctx);
```

---

## Summary of Issues and Recommendations

| # | Issue | Severity | File | Recommendation |
|---|-------|----------|------|----------------|
| 1 | **CRITICAL: PaymentSaga not registered** | **CRITICAL** | `PaymentService.API/Program.cs:57-83` | Add `x.AddSaga<PaymentSaga>()` |
| 2 | `IOTPVerified` may not reach Saga | **HIGH** | `PaymentSaga.cs:127` | Fix saga registration first |
| 3 | Saga publishes & consumes `IPaymentCompleted` | **MEDIUM** | `PaymentSaga.cs:110` + `PaymentConsumers.cs:10` | Remove consumer or rename |
| 4 | OTP stored in plaintext in Saga state | **MEDIUM** | `PaymentSagaState.cs:23` | Hash OTP before storage |
| 5 | BillingService missing `IPaymentFailed` | **MEDIUM** | `BillingService.API/Program.cs` | Add consumer or document |

---

*Analysis completed: 2026-03-31*
