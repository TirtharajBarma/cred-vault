# Database Enums Reference

This document lists all enums used in the project, the database/table they belong to, and their meanings.

---

## 1. Identity Service (identity-service)

### 1.1 UserRole Enum

**File:** `IdentityService.Domain/Enums/UserRole.cs`

```csharp
public enum UserRole
{
    User = 0,
    Admin = 1
}
```

| Value       | Meaning                        |
| ----------- | ------------------------------ |
| `0` (User)  | Regular customer user          |
| `1` (Admin) | Administrator with full access |

**Used in Column:** `Users.Role` (Users table in Identity DB)

---

### 1.2 UserStatus Enum

**File:** `IdentityService.Domain/Enums/UserStatus.cs`

```csharp
public enum UserStatus
{
    PendingVerification = 0,
    Active = 1,
    Suspended = 2,
    Blocked = 3,
    Deleted = 4
}
```

| Value                     | Meaning                                |
| ------------------------- | -------------------------------------- |
| `0` (PendingVerification) | User registered but email not verified |
| `1` (Active)              | Fully verified and active user         |
| `2` (Suspended)           | Temporarily suspended (can be revived) |
| `3` (Blocked)             | Permanently blocked (cannot login)     |
| `4` (Deleted)             | Soft-deleted user                      |

**Used in Column:** `Users.Status` (Users table in Identity DB)

---

## 2. Card Service (card-service)

### 2.1 CardNetwork Enum

**File:** `Shared.Contracts/Enums/CardNetwork.cs`

```csharp
public enum CardNetwork
{
    Unknown = 0,
    Visa = 1,
    Mastercard = 2
}
```

| Value            | Meaning                        |
| ---------------- | ------------------------------ |
| `0` (Unknown)    | Unknown or unsupported network |
| `1` (Visa)       | Visa card                      |
| `2` (Mastercard) | Mastercard                     |

**Used in Column:** `CardIssuers.Network` (CardIssuers table in Card DB)

---

### 2.2 CardTransactionType Enum

**File:** `CardService.Domain/Entities/CardTransaction.cs`

```csharp
public enum CardTransactionType
{
    Purchase = 0,
    Payment = 1,
    Refund = 2
}
```

| Value          | Meaning                                   |
| -------------- | ----------------------------------------- |
| `0` (Purchase) | Card was used to buy something            |
| `1` (Payment)  | Payment made to card (e.g., bill payment) |
| `2` (Refund)   | Money returned to card                    |

**Used in Column:** `CardTransactions.Type` (CardTransactions table in Card DB)

---

## 3. Billing Service (billing-service)

### 3.1 BillStatus Enum

**File:** `BillingService.Domain/Entities/Enums.cs`

```csharp
public enum BillStatus
{
    Pending = 0,
    Paid = 1,
    Overdue = 2,
    Cancelled = 3,
    PartiallyPaid = 4
}
```

| Value               | Meaning                             |
| ------------------- | ----------------------------------- |
| `0` (Pending)       | Bill generated, awaiting payment    |
| `1` (Paid)          | Full payment received               |
| `2` (Overdue)       | Payment not received after due date |
| `3` (Cancelled)     | Bill cancelled                      |
| `4` (PartiallyPaid) | Partial payment made                |

**Used in Column:** `Bills.Status` (Bills table in Billing DB)

---

### 3.2 StatementStatus Enum

**File:** `BillingService.Domain/Entities/Statement.cs`

```csharp
public enum StatementStatus
{
    Generated = 0,
    Paid = 1,
    Overdue = 2,
    PartiallyPaid = 3
}
```

| Value               | Meaning                                |
| ------------------- | -------------------------------------- |
| `0` (Generated)     | Statement generated for billing period |
| `1` (Paid)          | Full payment received                  |
| `2` (Overdue)       | Payment overdue                        |
| `3` (PartiallyPaid) | Partial payment made                   |

**Used in Column:** `Statements.Status` (Statements table in Billing DB)

---

### 3.3 RewardTransactionType Enum

**File:** `BillingService.Domain/Entities/Enums.cs`

```csharp
public enum RewardTransactionType
{
    Earned = 0,
    Adjusted = 1,
    Redeemed = 2,
    Reversed = 3
}
```

| Value          | Meaning                                    |
| -------------- | ------------------------------------------ |
| `0` (Earned)   | Points earned from transactions            |
| `1` (Adjusted) | Manual adjustment by admin                 |
| `2` (Redeemed) | Points used to pay bill                    |
| `3` (Reversed) | Previously earned/redeemed points reversed |

**Used in Column:** `RewardTransactions.Type` (RewardTransactions table in Billing DB)

---

### 3.4 RedeemRewardsTarget Enum

**File:** `BillingService.Application/Commands/Rewards/RedeemRewardsCommand.cs`

```csharp
public enum RedeemRewardsTarget
{
    Account = 1,
    Bill = 2
}
```

| Value         | Meaning                              |
| ------------- | ------------------------------------ |
| `1` (Account) | Redeem points to wallet/account      |
| `2` (Bill)    | Redeem points to pay a specific bill |

**Used in:** Command parameter (not stored in DB as enum)

---

## 4. Payment Service (payment-service)

### 4.1 PaymentStatus Enum

**File:** `PaymentService.Domain/Enums/Enums.cs`

```csharp
public enum PaymentStatus
{
    Initiated = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
    Reversed = 4,
    Cancelled = 5,
    Expired = 6
}
```

| Value            | Meaning                                    |
| ---------------- | ------------------------------------------ |
| `0` (Initiated)  | Payment started, awaiting OTP verification |
| `1` (Processing) | OTP verified, processing payment           |
| `2` (Completed)  | Payment successful                         |
| `3` (Failed)     | Payment failed                             |
| `4` (Reversed)   | Payment reversed/refunded                  |
| `5` (Cancelled)  | Payment cancelled by user                  |
| `6` (Expired)    | Payment timed out (e.g., OTP expired)      |

**Used in Column:** `Payments.Status` (Payments table in Payment DB)

---

### 4.2 PaymentType Enum

**File:** `PaymentService.Domain/Enums/Enums.cs`

```csharp
public enum PaymentType
{
    Full = 0,
    Partial = 1,
    Scheduled = 2
}
```

| Value           | Meaning                                |
| --------------- | -------------------------------------- |
| `0` (Full)      | Pay full bill amount                   |
| `1` (Partial)   | Pay partial amount (e.g., minimum due) |
| `2` (Scheduled) | Future scheduled payment               |

**Used in Column:** `Payments.PaymentType` (Payments table in Payment DB)

---

### 4.3 PaymentTransactionType Enum

**File:** `PaymentService.Domain/Enums/Enums.cs`

```csharp
public enum PaymentTransactionType
{
    Payment = 0,
    Reversal = 1
}
```

| Value          | Meaning                     |
| -------------- | --------------------------- |
| `0` (Payment)  | Regular payment transaction |
| `1` (Reversal) | Payment reversal/refund     |

**Used in Column:** `PaymentTransactions.Type` (PaymentTransactions table in Payment DB)

---

### 4.4 WalletTransactionType Enum

**File:** `PaymentService.Domain/Enums/WalletTransactionType.cs`

```csharp
public enum WalletTransactionType
{
    TopUp = 1,
    Payment = 2,
    Refund = 3,
    Withdrawal = 4
}
```

| Value            | Meaning                    |
| ---------------- | -------------------------- |
| `1` (TopUp)      | User added money to wallet |
| `2` (Payment)    | Money deducted for payment |
| `3` (Refund)     | Money returned to wallet   |
| `4` (Withdrawal) | User withdrew from wallet  |

**Used in Column:** `WalletTransactions.Type` (WalletTransactions table in Payment DB)

---

## Summary Table

| Service  | Enum                   | Database | Table               | Values                                                                 |
| -------- | ---------------------- | -------- | ------------------- | ---------------------------------------------------------------------- |
| Identity | UserRole               | Identity | Users               | User, Admin                                                            |
| Identity | UserStatus             | Identity | Users               | PendingVerification, Active, Suspended, Blocked, Deleted               |
| Card     | CardNetwork            | Card     | CardIssuers         | Unknown, Visa, Mastercard                                              |
| Card     | CardTransactionType    | Card     | CardTransactions    | Purchase, Payment, Refund                                              |
| Billing  | BillStatus             | Billing  | Bills               | Pending, Paid, Overdue, Cancelled, PartiallyPaid                       |
| Billing  | StatementStatus        | Billing  | Statements          | Generated, Paid, Overdue, PartiallyPaid                                |
| Billing  | RewardTransactionType  | Billing  | RewardTransactions  | Earned, Adjusted, Redeemed, Reversed                                   |
| Payment  | PaymentStatus          | Payment  | Payments            | Initiated, Processing, Completed, Failed, Reversed, Cancelled, Expired |
| Payment  | PaymentType            | Payment  | Payments            | Full, Partial, Scheduled                                               |
| Payment  | PaymentTransactionType | Payment  | PaymentTransactions | Payment, Reversal                                                      |
| Payment  | WalletTransactionType  | Payment  | WalletTransactions  | TopUp, Payment, Refund, Withdrawal                                     |

---

## Notes

- All enums are stored as integers in the database columns
- The string representations shown in UI are derived from the enum values
- Some enums like `RedeemRewardsTarget` are used as command parameters but not persisted as enum in DB
