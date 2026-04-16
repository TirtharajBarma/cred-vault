# CredVault Credit Card Platform - Complete System Documentation

## Admin Credentials
- **Email:** tirtharajbarma3@gmail.com
- **Password:** dominos7

## Base URL
All endpoints are accessed via Gateway: `http://localhost:5006`

---

# PART 1: ALL ENDPOINTS & ROUTES

## Gateway Routes Summary
| Service | Port | Routes Count |
|---------|------|--------------|
| Identity | 5001 | 9 routes |
| Cards | 5002 | 8 routes |
| Issuers | 5002 | 2 routes |
| Billing (Bills) | 5003 | 5 routes |
| Billing (Statements) | 5003 | 6 routes |
| Billing (Rewards) | 5003 | 5 routes |
| Payments | 5004 | 6 routes |
| Wallets | 5004 | 5 routes |
| Notifications | 5005 | 4 routes |

---

# PART 2: IDENTITY SERVICE (Port 5001 via Gateway: /api/v1/identity)

## Authentication Endpoints

### POST /api/v1/identity/auth/register
**Purpose:** Register new user
```json
Request:
{
  "fullName": "John Doe",
  "email": "john@example.com",
  "password": "securepassword123"
}

Response (201):
{
  "success": true,
  "data": {
    "user": { "id": "uuid", "email": "...", "role": "user", "status": "pending-verification" },
    "accessToken": "eyJ..."
  }
}
```

### POST /api/v1/identity/auth/login
**Purpose:** Login with email/password
```json
Request:
{
  "email": "john@example.com",
  "password": "securepassword123"
}

Response (200):
{
  "success": true,
  "data": {
    "user": { ... },
    "accessToken": "eyJ..."
  }
}
```

### POST /api/v1/identity/auth/verify-email-otp
**Purpose:** Verify email with OTP
```json
Request:
{
  "email": "john@example.com",
  "otp": "123456"
}
```

### POST /api/v1/identity/auth/forgot-password
**Purpose:** Request password reset OTP
```json
Request:
{ "email": "john@example.com" }
```

### POST /api/v1/identity/auth/reset-password
**Purpose:** Complete password reset
```json
Request:
{
  "email": "john@example.com",
  "otp": "123456",
  "newPassword": "newsecurepassword123"
}
```

## User Management Endpoints

### GET /api/v1/identity/users/me
**Auth:** Required
**Purpose:** Get current user profile

### PUT /api/v1/identity/users/me
**Auth:** Required
```json
Request:
{ "fullName": "John Smith" }
```

### PUT /api/v1/identity/users/me/password
**Auth:** Required
```json
Request:
{
  "currentPassword": "oldpassword",
  "newPassword": "newpassword"
}
```

### GET /api/v1/identity/users/{userId}
**Auth:** Admin only
**Purpose:** Get any user by ID

### PUT /api/v1/identity/users/{userId}/status
**Auth:** Admin only
```json
Request:
{ "status": "active|suspended|blocked|pending-verification" }
```

### PUT /api/v1/identity/users/{userId}/role
**Auth:** Admin only
```json
Request:
{ "role": "user|admin" }
```

### GET /api/v1/identity/users
**Auth:** Admin only
**Purpose:** List all users (paginated)

### GET /api/v1/identity/users/stats
**Auth:** Admin only
**Purpose:** Get user statistics

---

# PART 3: CARD SERVICE (Port 5002 via Gateway: /api/v1/cards)

## Card Endpoints

### POST /api/v1/cards
**Auth:** Required (User)
**Purpose:** Create new card
```json
Request:
{
  "cardholderName": "John Doe",
  "expMonth": 12,
  "expYear": 2028,
  "cardNumber": "4111111111111111",
  "issuerId": "guid",
  "isDefault": true
}
```

### GET /api/v1/cards
**Auth:** Required (User)
**Purpose:** List all user's cards

### GET /api/v1/cards/{cardId}
**Auth:** Required (User)
**Purpose:** Get specific card details

### PUT /api/v1/cards/{cardId}
**Auth:** Required (User)
**Purpose:** Update card (name, expiry, default)

### DELETE /api/v1/cards/{cardId}
**Auth:** Required (User)
**Purpose:** Delete card (soft delete)

### POST /api/v1/cards/{cardId}/transactions
**Auth:** Required (User)
**Purpose:** Add transaction to card
```json
Request:
{
  "type": 1,  // 0=Purchase, 1=Payment, 2=Refund
  "amount": 1000.00,
  "description": "Purchase at store",
  "dateUtc": "2026-04-10T00:00:00Z"
}
```

### GET /api/v1/cards/{cardId}/transactions
**Auth:** Required (User)
**Purpose:** List card transactions

### PUT /api/v1/cards/{cardId}/admin
**Auth:** Admin only
**Purpose:** Update card limits/balance
```json
Request:
{
  "creditLimit": 50000,
  "outstandingBalance": 2000,
  "billingCycleStartDay": 15
}
```

### GET /api/v1/cards/admin/{cardId}
**Auth:** Admin only
**Purpose:** Get any card

### GET /api/v1/cards/user/{userId}
**Auth:** Admin only
**Purpose:** Get all cards for user

## Issuer Endpoints

### GET /api/v1/issuers
**Auth:** Required (User)
**Purpose:** List all issuers

### POST /api/v1/issuers
**Auth:** Admin only
```json
Request:
{
  "name": "Test Bank",
  "network": "Visa"  // or "Mastercard"
}
```

### PUT /api/v1/issuers/{id}
**Auth:** Admin only
**Purpose:** Update issuer

### DELETE /api/v1/issuers/{id}
**Auth:** Admin only
**Purpose:** Delete issuer

---

# PART 4: BILLING SERVICE (Port 5003 via Gateway: /api/v1/billing)

## Bill Endpoints

### GET /api/v1/billing/bills
**Auth:** Required (User/Admin)
**Purpose:** List bills for user

### GET /api/v1/billing/bills/{billId}
**Auth:** Required (User)
**Purpose:** Get bill details

### POST /api/v1/billing/bills/admin/generate-bill
**Auth:** Admin only
```json
Request:
{
  "userId": "guid",
  "cardId": "guid",
  "currency": "INR",
  "billingCycleStartDay": 15
}
```

### GET /api/v1/billing/bills/has-pending/{cardId}
**Purpose:** Check if card has pending bill

### POST /api/v1/billing/bills/admin/check-overdue
**Auth:** Admin only
**Purpose:** Check and mark overdue bills

## Statement Endpoints

### GET /api/v1/billing/statements
**Auth:** Required
**Purpose:** List statements

### GET /api/v1/billing/statements/{statementId}
**Auth:** Required
**Purpose:** Get statement with transactions

### GET /api/v1/billing/statements/bill/{billId}
**Auth:** Required
**Purpose:** Get statements for a bill

## Reward Endpoints

### GET /api/v1/billing/rewards/tiers
**Auth:** Required
**Purpose:** List all reward tiers

### POST /api/v1/billing/rewards/tiers
**Auth:** Admin only
```json
Request:
{
  "cardNetwork": "Visa",
  "issuerId": "guid (optional)",
  "minSpend": 500,
  "rewardRate": 0.02,  // 2% cashback
  "effectiveFromUtc": "2026-01-01T00:00:00Z"
}
```

### PUT /api/v1/billing/rewards/tiers/{id}
**Auth:** Admin only
**Purpose:** Update reward tier

### DELETE /api/v1/billing/rewards/tiers/{id}
**Auth:** Admin only
**Purpose:** Delete reward tier

### GET /api/v1/billing/rewards/account
**Auth:** Required
**Purpose:** Get user's reward account

### GET /api/v1/billing/rewards/transactions
**Auth:** Required
**Purpose:** Get user's reward history

---

# PART 5: PAYMENT SERVICE (Port 5004 via Gateway: /api/v1/payments)

## Payment Endpoints

### POST /api/v1/payments/initiate
**Auth:** Required (User)
**Purpose:** Start payment process
```json
Request:
{
  "cardId": "guid",
  "billId": "guid",
  "amount": 1000.00,
  "paymentType": "Full",  // "Full", "Partial"
  "rewardsPoints": 100  // Optional
}

Response:
{
  "success": true,
  "data": {
    "paymentId": "guid",
    "otpRequired": true,
    "rewardsApplied": true,
    "rewardsAmount": 25.00,
    "finalAmount": 975.00
  }
}
```

### POST /api/v1/payments/{paymentId}/verify-otp
**Auth:** Required (User)
**Purpose:** Verify OTP to complete payment
```json
Request:
{ "otpCode": "123456" }
```

### POST /api/v1/payments/{paymentId}/resend-otp
**Auth:** Required (User)
**Purpose:** Resend OTP

### GET /api/v1/payments
**Auth:** Required (User)
**Purpose:** List user's payments

### GET /api/v1/payments/{paymentId}
**Auth:** Required (User)
**Purpose:** Get payment details

### GET /api/v1/payments/{paymentId}/transactions
**Auth:** Required (User)
**Purpose:** Get payment transactions

---

# PART 6: WALLET SERVICE (Port 5004 via Gateway: /api/v1/wallets)

## Wallet Endpoints

### GET /api/v1/wallets/me
**Auth:** Required (User)
**Purpose:** Get user's wallet
```json
Response:
{
  "success": true,
  "data": {
    "hasWallet": true,
    "walletId": "guid",
    "balance": 5000.00,
    "createdAtUtc": "2026-04-01T00:00:00Z"
  }
}
```

### POST /api/v1/wallets/topup
**Auth:** Required (User)
**Purpose:** Add money to wallet
```json
Request:
{
  "amount": 5000.00,
  "description": "Added funds"
}
```

### GET /api/v1/wallets/balance
**Auth:** Required (User)
**Purpose:** Get wallet balance

### GET /api/v1/wallets/transactions
**Auth:** Required (User)
**Purpose:** Get wallet transactions

---

# PART 7: RABBITMQ EVENTS & SAGA FLOW

## Event Flow Diagram

### Happy Path (Successful Payment):
```
1. POST /payments/initiate
   → Creates Payment record
   → Publishes IStartPaymentOrchestration
   → Publishes IPaymentOtpGenerated (email OTP)
   
2. Saga enters: AwaitingOtpVerification
   
3. POST /payments/{id}/verify-otp
   → Publishes IOtpVerified
   → Saga transitions to AwaitingPaymentConfirmation
   
4. PaymentProcessConsumer receives IPaymentProcessRequested
   → Deducts from WALLET (not card!)
   → Publishes IPaymentProcessSucceeded
   → Saga enters AwaitingBillUpdate
   
5. BillUpdateSagaConsumer receives IBillUpdateRequested
   → Marks bill as Paid/PartiallyPaid
   → Publishes IBillUpdateSucceeded
   → If rewards: Saga enters AwaitingRewardRedemption
   → If no rewards: Saga enters AwaitingCardDeduction
   
6. (If rewards) RewardRedemptionConsumer
   → Redeems reward points
   → Publishes IRewardRedemptionSucceeded
   
7. CardDeductionSagaConsumer receives ICardDeductionRequested
   → Reduces card's OutstandingBalance
   → Publishes ICardDeductionSucceeded
   → Saga enters Completed
   
8. IPaymentCompleted published
   → Payment marked as Completed
```

### Compensation Path (Failure/Rollback):
```
1. Any step fails
   → Saga enters Compensating state
   
2. RevertBillSagaConsumer: IRevertBillUpdateRequested
   → Reverts bill status
   → Publishes IRevertBillUpdateSucceeded
   
3. RevertPaymentConsumer: IRevertPaymentRequested
   → Updates payment status
   → Publishes IRevertPaymentSucceeded
   
4. WalletRefundConsumer: IWalletRefundRequested
   → REFUNDS MONEY BACK TO WALLET
   → Publishes IWalletRefundSucceeded
   
5. Saga enters Compensated
   → IPaymentFailed published with refund info
```

## Key Events Summary

| Event | Direction | Purpose |
|-------|-----------|---------|
| IUserRegistered | Identity → All | User created, wallet auto-created |
| IStartPaymentOrchestration | Payment → Saga | Start payment saga |
| IOtpVerified | Payment → Saga | OTP verified |
| IPaymentProcessRequested | Saga → Consumer | Wallet deduction request |
| IBillUpdateRequested | Saga → Billing | Update bill to paid |
| ICardDeductionRequested | Saga → Card | Reduce outstanding balance |
| IWalletRefundRequested | Saga → Payment | Refund wallet on failure |
| IPaymentCompleted | Saga → All | Payment successful |
| IPaymentFailed | Saga → All | Payment failed |

---

# PART 8: DATABASE TABLES

## credvault_identity
### identity_users
| Column | Type |
|--------|------|
| Id | uniqueidentifier |
| Email | nvarchar |
| FullName | nvarchar |
| PasswordHash | nvarchar |
| IsEmailVerified | bit |
| Status | nvarchar |
| Role | nvarchar |
| CreatedAtUtc | datetime2 |

## credvault_cards
### CreditCards
| Column | Type |
|--------|------|
| Id | uniqueidentifier |
| UserId | uniqueidentifier |
| IssuerId | uniqueidentifier |
| CardholderName | nvarchar |
| ExpMonth | int |
| ExpYear | int |
| Last4 | nvarchar |
| CreditLimit | decimal |
| OutstandingBalance | decimal |
| BillingCycleStartDay | int |
| IsDefault | bit |

### CardIssuers
| Column | Type |
|--------|------|
| Id | uniqueidentifier |
| Name | nvarchar |
| Network | int (1=Visa, 2=Mastercard) |

### CardTransactions
| Column | Type |
|--------|------|
| Id | uniqueidentifier |
| CardId | uniqueidentifier |
| UserId | uniqueidentifier |
| Type | int (0=Purchase, 1=Payment, 2=Refund) |
| Amount | decimal |
| Description | nvarchar |

## credvault_billing
### Bills
| Column | Type |
|--------|------|
| Id | uniqueidentifier |
| UserId | uniqueidentifier |
| CardId | uniqueidentifier |
| Amount | decimal |
| MinDue | decimal |
| AmountPaid | decimal |
| Status | int (0=Pending, 1=Paid, 2=Overdue, 3=Cancelled, 4=PartiallyPaid) |

### RewardTiers
| Column | Type |
|--------|------|
| Id | uniqueidentifier |
| CardNetwork | int |
| IssuerId | uniqueidentifier |
| MinSpend | decimal |
| RewardRate | decimal |

### RewardAccounts
| Column | Type |
|--------|------|
| Id | uniqueidentifier |
| UserId | uniqueidentifier |
| RewardTierId | uniqueidentifier |
| PointsBalance | decimal |

## credvault_payments
### Payments
| Column | Type |
|--------|------|
| Id | uniqueidentifier |
| UserId | uniqueidentifier |
| CardId | uniqueidentifier |
| BillId | uniqueidentifier |
| Amount | decimal |
| PaymentType | int |
| Status | int |
| OtpCode | nvarchar |
| OtpExpiresAtUtc | datetime2 |

### UserWallets
| Column | Type |
|--------|------|
| Id | uniqueidentifier |
| UserId | uniqueidentifier |
| Balance | decimal |
| TotalTopUps | decimal |
| TotalSpent | decimal |

### WalletTransactions
| Column | Type |
|--------|------|
| Id | uniqueidentifier |
| WalletId | uniqueidentifier |
| Type | int (0=TopUp, 1=Payment, 2=Refund) |
| Amount | decimal |
| BalanceAfter | decimal |
| RelatedPaymentId | uniqueidentifier |

### PaymentOrchestrationSagas
| Column | Type |
|--------|------|
| CorrelationId | uniqueidentifier |
| CurrentState | nvarchar |
| PaymentId | uniqueidentifier |
| UserId | uniqueidentifier |
| BillId | uniqueidentifier |
| CardId | uniqueidentifier |
| Amount | decimal |
| WalletDeducted | bit |
| OtpVerified | bit |
| PaymentProcessed | bit |
| BillUpdated | bit |
| CardDeducted | bit |

---

# PART 9: TEST PLAN

## Admin Setup Commands
```bash
# Login as admin
ADMIN_TOKEN=$(curl -s -X POST http://localhost:5006/api/v1/identity/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email": "tirtharajbarma3@gmail.com", "password": "dominos7"}' | jq -r '.data.accessToken')

echo "Admin Token: ${ADMIN_TOKEN:0:50}..."
```

## Test Sequence

### PHASE 1: Admin Setup
1. Login as admin
2. Create card issuer (Visa/Mastercard)
3. Set reward tier for card network

### PHASE 2: User Registration & Wallet
1. Register new user
2. Verify email OTP
3. Login user
4. Check wallet auto-created
5. Top up wallet

### PHASE 3: Card & Transaction
1. Create card for user
2. Admin: Set credit limit
3. Add purchase transaction (increases outstanding balance)
4. Verify outstanding balance updated

### PHASE 4: Bill Generation & Rewards
1. Admin: Generate bill
2. Check bill status (Pending)
3. Check reward account created
4. Admin: Set reward tier with good rate

### PHASE 5: Payment - Happy Path (Without Rewards)
1. Initiate payment (Full, no rewards)
2. Get OTP from database
3. Verify OTP
4. Check saga states:
   - AwaitingBillUpdate
   - AwaitingCardDeduction
   - Completed
5. Verify:
   - Bill status = Paid
   - Card outstanding balance reduced
   - Wallet balance reduced
   - Payment status = Completed

### PHASE 6: Payment - Happy Path (With Rewards)
1. Add another transaction to create new bill
2. Generate new bill
3. Initiate payment with rewards
4. Verify OTP
5. Verify:
   - Rewards redeemed
   - Bill paid with rewards
   - Reward balance reduced

### PHASE 7: Payment - Rollback/Compensation
1. Create payment and verify OTP
2. (Simulate failure by checking refund logic)
3. Verify:
   - Wallet refunded
   - Bill reverted
   - Card balance restored
   - Saga state = Compensated
   - Payment status = Failed

### PHASE 8: End-to-End Validation
1. List all user's cards
2. List all user's bills
3. List all user's payments
4. List wallet transactions
5. Verify data consistency across services

---

# PART 10: SQL QUERIES FOR VERIFICATION

## Get OTP from Database
```sql
-- Get payment OTP (check Payments table)
SELECT TOP 1 Id, OtpCode, OtpExpiresAtUtc, Status 
FROM Payments 
ORDER BY CreatedAtUtc DESC;

-- Or check notification logs for latest OTP email
SELECT TOP 10 CreatedAtUtc, Subject, Recipient 
FROM NotificationLogs 
WHERE Subject LIKE '%Payment%OTP%' 
ORDER BY CreatedAtUtc DESC;
```

## Check Wallet Balance
```sql
SELECT * FROM UserWallets WHERE UserId = 'user-guid';
SELECT * FROM WalletTransactions WHERE WalletId = 'wallet-guid' ORDER BY CreatedAtUtc DESC;
```

## Check Card Balance
```sql
SELECT Id, CreditLimit, OutstandingBalance 
FROM CreditCards 
WHERE UserId = 'user-guid';
```

## Check Bill Status
```sql
SELECT Id, Amount, AmountPaid, Status 
FROM Bills 
WHERE UserId = 'user-guid' 
ORDER BY CreatedAtUtc DESC;
```

## Check Saga State
```sql
SELECT CorrelationId, CurrentState, PaymentId, 
       WalletDeducted, BillUpdated, CardDeducted 
FROM PaymentOrchestrationSagas 
WHERE PaymentId = 'payment-guid';
```

## Check Rewards
```sql
SELECT * FROM RewardAccounts WHERE UserId = 'user-guid';
SELECT * FROM RewardTransactions WHERE UserId = 'user-guid' ORDER BY CreatedAtUtc DESC;
```
