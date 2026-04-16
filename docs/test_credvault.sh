#!/bin/bash
# CredVault System Test Script
# Usage: ./test_credvault.sh

set -e

BASE_URL="http://localhost:5006"
GATEWAY="$BASE_URL"

echo "=========================================="
echo "CredVault System Test Script"
echo "=========================================="
echo ""

# ============================================
# PHASE 1: ADMIN SETUP
# ============================================
echo ">>> PHASE 1: Admin Login & Setup"
echo "------------------------------------------"

ADMIN_RESP=$(curl -s -X POST "$GATEWAY/api/v1/identity/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email": "tirtharajbarma3@gmail.com", "password": "dominos7"}')

ADMIN_TOKEN=$(echo "$ADMIN_RESP" | jq -r '.data.accessToken')
if [ "$ADMIN_TOKEN" == "null" ] || [ -z "$ADMIN_TOKEN" ]; then
  echo "❌ Admin login failed!"
  echo "$ADMIN_RESP" | jq .
  exit 1
fi
echo "✅ Admin logged in: ${ADMIN_TOKEN:0:50}..."
echo ""

# Create Card Issuer
echo ">>> Creating Visa Issuer..."
ISSUER_RESP=$(curl -s -X POST "$GATEWAY/api/v1/issuers" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name": "Test Visa Bank", "network": "Visa"}')
ISSUER_ID=$(echo "$ISSUER_RESP" | jq -r '.data.id // empty')
if [ -z "$ISSUER_ID" ]; then
  echo "Issuer may already exist, getting existing..."
  ISSUER_RESP=$(curl -s "$GATEWAY/api/v1/issuers" -H "Authorization: Bearer $ADMIN_TOKEN")
  ISSUER_ID=$(echo "$ISSUER_RESP" | jq -r '.data[0].id // empty')
fi
echo "✅ Issuer ID: $ISSUER_ID"

# Create Reward Tier
echo ">>> Creating Reward Tier..."
TIER_RESP=$(curl -s -X POST "$GATEWAY/api/v1/billing/rewards/tiers" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"cardNetwork\": \"Visa\",
    \"issuerId\": \"$ISSUER_ID\",
    \"minSpend\": 100,
    \"rewardRate\": 0.02,
    \"effectiveFromUtc\": \"2026-01-01T00:00:00Z\"
  }")
TIER_ID=$(echo "$TIER_RESP" | jq -r '.data.id // empty')
echo "✅ Reward Tier ID: $TIER_ID"
echo ""

# ============================================
# PHASE 2: USER REGISTRATION & WALLET
# ============================================
echo ">>> PHASE 2: User Registration & Wallet"
echo "------------------------------------------"

# Register new user
USER_EMAIL="testuser$(date +%s)@example.com"
USER_PASS="TestPass123!"
USER_FULL="Test User $(date +%s)"

echo ">>> Registering user: $USER_EMAIL"
REG_RESP=$(curl -s -X POST "$GATEWAY/api/v1/identity/auth/register" \
  -H "Content-Type: application/json" \
  -d "{
    \"fullName\": \"$USER_FULL\",
    \"email\": \"$USER_EMAIL\",
    \"password\": \"$USER_PASS\"
  }")

USER_ID=$(echo "$REG_RESP" | jq -r '.data.user.id // empty')
TEMP_TOKEN=$(echo "$REG_RESP" | jq -r '.data.accessToken // empty')

if [ -z "$USER_ID" ]; then
  echo "❌ Registration may have failed (checking if user exists)..."
  # Try to login anyway
  LOGIN_RESP=$(curl -s -X POST "$GATEWAY/api/v1/identity/auth/login" \
    -H "Content-Type: application/json" \
    -d "{\"email\": \"$USER_EMAIL\", \"password\": \"$USER_PASS\"}")
  USER_ID=$(echo "$LOGIN_RESP" | jq -r '.data.user.id // empty')
  TEMP_TOKEN=$(echo "$LOGIN_RESP" | jq -r '.data.accessToken // empty')
fi

echo "✅ User ID: $USER_ID"
echo "✅ Temp Token: ${TEMP_TOKEN:0:50}..."

# Verify email (get OTP from DB)
echo ">>> Verifying email..."
OTP_DB_RESP=$(docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Sql@Password!123' -C -d credvault_identity -Q "SELECT TOP 1 EmailVerificationOtp FROM identity_users WHERE Id = '$USER_ID'" 2>/dev/null || echo "")
OTP_CODE=$(echo "$OTP_DB_RESP" | grep -o '[0-9]\{6\}' | head -1)

if [ -z "$OTP_CODE" ]; then
  echo "⚠️  Could not get OTP from DB, attempting login anyway..."
else
  echo ">>> OTP from DB: $OTP_CODE"
  VERIFY_RESP=$(curl -s -X POST "$GATEWAY/api/v1/identity/auth/verify-email-otp" \
    -H "Content-Type: application/json" \
    -d "{\"email\": \"$USER_EMAIL\", \"otp\": \"$OTP_CODE\"}")
  echo "✅ Email verified"
fi

# Login user
echo ">>> Logging in user..."
LOGIN_RESP=$(curl -s -X POST "$GATEWAY/api/v1/identity/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\": \"$USER_EMAIL\", \"password\": \"$USER_PASS\"}")

USER_TOKEN=$(echo "$LOGIN_RESP" | jq -r '.data.accessToken // empty')
if [ -z "$USER_TOKEN" ] || [ "$USER_TOKEN" == "null" ]; then
  echo "❌ User login failed!"
  echo "$LOGIN_RESP" | jq .
  exit 1
fi
echo "✅ User Token: ${USER_TOKEN:0:50}..."

# Check wallet
echo ">>> Checking wallet..."
WALLET_RESP=$(curl -s "$GATEWAY/api/v1/wallets/me" \
  -H "Authorization: Bearer $USER_TOKEN")
WALLET_ID=$(echo "$WALLET_RESP" | jq -r '.data.walletId // empty')
echo "✅ Wallet ID: $WALLET_ID"

# Top up wallet
echo ">>> Topping up wallet with ₹5000..."
TOPUP_RESP=$(curl -s -X POST "$GATEWAY/api/v1/wallets/topup" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"amount": 5000, "description": "Initial top-up for testing"}')
echo "$TOPUP_RESP" | jq .

WALLET_BALANCE=$(echo "$TOPUP_RESP" | jq -r '.data.newBalance // 0')
echo "✅ Wallet Balance: ₹$WALLET_BALANCE"
echo ""

# ============================================
# PHASE 3: CARD CREATION & TRANSACTION
# ============================================
echo ">>> PHASE 3: Card Creation & Transaction"
echo "------------------------------------------"

# Create card
echo ">>> Creating card..."
CARD_RESP=$(curl -s -X POST "$GATEWAY/api/v1/cards" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"cardholderName\": \"$USER_FULL\",
    \"expMonth\": 12,
    \"expYear\": 2028,
    \"cardNumber\": \"4111111111111111\",
    \"issuerId\": \"$ISSUER_ID\",
    \"isDefault\": true
  }")

CARD_ID=$(echo "$CARD_RESP" | jq -r '.data.id // empty')
if [ -z "$CARD_ID" ] || [ "$CARD_ID" == "null" ]; then
  echo "❌ Card creation failed!"
  echo "$CARD_RESP" | jq .
  exit 1
fi
echo "✅ Card ID: $CARD_ID"

# Admin: Set credit limit
echo ">>> Admin: Setting credit limit to ₹50000..."
curl -s -X PUT "$GATEWAY/api/v1/cards/$CARD_ID/admin" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"creditLimit": 50000, "outstandingBalance": 0}' | jq .

# Add purchase transaction (simulates spending)
echo ">>> Adding purchase transaction ₹2000..."
TX_RESP=$(curl -s -X POST "$GATEWAY/api/v1/cards/$CARD_ID/transactions" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "type": 0,
    "amount": 2000,
    "description": "Test purchase",
    "dateUtc": "2026-04-10T00:00:00Z"
  }')
echo "$TX_RESP" | jq .

# Verify outstanding balance
echo ">>> Verifying card balance..."
CARD_DETAIL=$(curl -s "$GATEWAY/api/v1/cards/$CARD_ID" \
  -H "Authorization: Bearer $USER_TOKEN")
OUTSTANDING=$(echo "$CARD_DETAIL" | jq -r '.data.outstandingBalance // 0')
echo "✅ Outstanding Balance: ₹$OUTSTANDING"
echo ""

# ============================================
# PHASE 4: BILL GENERATION
# ============================================
echo ">>> PHASE 4: Bill Generation"
echo "------------------------------------------"

# Generate bill (admin)
echo ">>> Admin: Generating bill..."
BILL_RESP=$(curl -s -X POST "$GATEWAY/api/v1/billing/bills/admin/generate-bill" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"userId\": \"$USER_ID\",
    \"cardId\": \"$CARD_ID\",
    \"currency\": \"INR\",
    \"billingCycleStartDay\": 15
  }")

BILL_ID=$(echo "$BILL_RESP" | jq -r '.data.id // empty')
BILL_AMOUNT=$(echo "$BILL_RESP" | jq -r '.data.amount // 0')
BILL_STATUS=$(echo "$BILL_RESP" | jq -r '.data.status // empty')

if [ -z "$BILL_ID" ] || [ "$BILL_ID" == "null" ]; then
  echo "❌ Bill generation may have failed"
  echo "$BILL_RESP" | jq .
else
  echo "✅ Bill ID: $BILL_ID"
  echo "✅ Bill Amount: ₹$BILL_AMOUNT"
  echo "✅ Bill Status: $BILL_STATUS"
fi

# Check rewards account
echo ">>> Checking rewards account..."
REWARDS_RESP=$(curl -s "$GATEWAY/api/v1/billing/rewards/account" \
  -H "Authorization: Bearer $USER_TOKEN")
echo "$REWARDS_RESP" | jq .
echo ""

# ============================================
# PHASE 5: PAYMENT WITHOUT REWARDS
# ============================================
echo ">>> PHASE 5: Payment Without Rewards"
echo "------------------------------------------"

if [ -z "$BILL_ID" ] || [ "$BILL_ID" == "null" ]; then
  echo "⚠️  Skipping payment - no bill available"
else
  # Initiate payment
  echo ">>> Initiating payment..."
  PAY_RESP=$(curl -s -X POST "$GATEWAY/api/v1/payments/initiate" \
    -H "Authorization: Bearer $USER_TOKEN" \
    -H "Content-Type: application/json" \
    -d "{
      \"cardId\": \"$CARD_ID\",
      \"billId\": \"$BILL_ID\",
      \"amount\": $BILL_AMOUNT,
      \"paymentType\": \"Full\"
    }")

  PAYMENT_ID=$(echo "$PAY_RESP" | jq -r '.data.paymentId // empty')
  echo "$PAY_RESP" | jq .

  if [ -z "$PAYMENT_ID" ] || [ "$PAYMENT_ID" == "null" ]; then
    echo "❌ Payment initiation failed!"
  else
    echo "✅ Payment ID: $PAYMENT_ID"

    # Get OTP from database
    echo ">>> Getting OTP from database..."
    OTP_DB=$(docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Sql@Password!123' -C -d credvault_payments -Q "SELECT TOP 1 OtpCode FROM Payments WHERE Id = '$PAYMENT_ID'" 2>/dev/null || echo "")
    OTP=$(echo "$OTP_DB" | grep -o '[0-9]\{6\}' | head -1)
    echo "✅ OTP Code: $OTP"

    # Verify OTP
    echo ">>> Verifying OTP..."
    VERIFY_RESP=$(curl -s -X POST "$GATEWAY/api/v1/payments/$PAYMENT_ID/verify-otp" \
      -H "Authorization: Bearer $USER_TOKEN" \
      -H "Content-Type: application/json" \
      -d "{\"otpCode\": \"$OTP\"}")
    echo "$VERIFY_RESP" | jq .

    # Wait for saga to complete
    echo ">>> Waiting 5 seconds for saga to complete..."
    sleep 5

    # Check final states
    echo ">>> Checking final states..."

    # Check payment
    echo "--- Payment Status ---"
    curl -s "$GATEWAY/api/v1/payments/$PAYMENT_ID" \
      -H "Authorization: Bearer $USER_TOKEN" | jq .

    # Check wallet balance
    echo "--- Wallet Balance ---"
    curl -s "$GATEWAY/api/v1/wallets/me" \
      -H "Authorization: Bearer $USER_TOKEN" | jq .

    # Check card balance
    echo "--- Card Balance ---"
    curl -s "$GATEWAY/api/v1/cards/$CARD_ID" \
      -H "Authorization: Bearer $USER_TOKEN" | jq '.data.outstandingBalance, .data.creditLimit'

    # Check bill
    echo "--- Bill Status ---"
    curl -s "$GATEWAY/api/v1/billing/bills/$BILL_ID" \
      -H "Authorization: Bearer $USER_TOKEN" | jq '.data.id, .data.status, .data.amount, .data.amountPaid'

    # Check saga state
    echo "--- Saga State ---"
    docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Sql@Password!123' -C -d credvault_payments -Q "SELECT CurrentState, WalletDeducted, BillUpdated, CardDeducted FROM PaymentOrchestrationSagas WHERE PaymentId = '$PAYMENT_ID'" 2>/dev/null || echo ""
  fi
fi
echo ""

# ============================================
# PHASE 6: PAYMENT WITH REWARDS (Second Bill)
# ============================================
echo ">>> PHASE 6: Payment With Rewards"
echo "------------------------------------------"

# Add another transaction
echo ">>> Adding another purchase ₹3000..."
TX_RESP2=$(curl -s -X POST "$GATEWAY/api/v1/cards/$CARD_ID/transactions" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "type": 0,
    "amount": 3000,
    "description": "Second purchase",
    "dateUtc": "2026-04-11T00:00:00Z"
  }')

# Generate second bill
echo ">>> Admin: Generating second bill..."
BILL_RESP2=$(curl -s -X POST "$GATEWAY/api/v1/billing/bills/admin/generate-bill" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"userId\": \"$USER_ID\",
    \"cardId\": \"$CARD_ID\",
    \"currency\": \"INR\",
    \"billingCycleStartDay\": 15
  }")

BILL_ID2=$(echo "$BILL_RESP2" | jq -r '.data.id // empty')
BILL_AMOUNT2=$(echo "$BILL_RESP2" | jq -r '.data.amount // 0')

if [ -z "$BILL_ID2" ] || [ "$BILL_ID2" == "null" ]; then
  echo "⚠️  Second bill generation failed"
else
  echo "✅ Bill ID: $BILL_ID2, Amount: ₹$BILL_AMOUNT2"

  # Check rewards balance
  echo ">>> Current rewards balance..."
  curl -s "$GATEWAY/api/v1/billing/rewards/account" \
    -H "Authorization: Bearer $USER_TOKEN" | jq .

  # Initiate payment with rewards
  echo ">>> Initiating payment with rewards..."
  PAY_RESP2=$(curl -s -X POST "$GATEWAY/api/v1/payments/initiate" \
    -H "Authorization: Bearer $USER_TOKEN" \
    -H "Content-Type: application/json" \
    -d "{
      \"cardId\": \"$CARD_ID\",
      \"billId\": \"$BILL_ID2\",
      \"amount\": $BILL_AMOUNT2,
      \"paymentType\": \"Full\"
    }")

  PAYMENT_ID2=$(echo "$PAY_RESP2" | jq -r '.data.paymentId // empty')
  echo "$PAY_RESP2" | jq .

  if [ -n "$PAYMENT_ID2" ] && [ "$PAYMENT_ID2" != "null" ]; then
    echo "✅ Payment ID: $PAYMENT_ID2"

    # Get OTP
    OTP_DB2=$(docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Sql@Password!123' -C -d credvault_payments -Q "SELECT TOP 1 OtpCode FROM Payments WHERE Id = '$PAYMENT_ID2'" 2>/dev/null || echo "")
    OTP2=$(echo "$OTP_DB2" | grep -o '[0-9]\{6\}' | head -1)
    echo "✅ OTP: $OTP2"

    # Verify OTP
    curl -s -X POST "$GATEWAY/api/v1/payments/$PAYMENT_ID2/verify-otp" \
      -H "Authorization: Bearer $USER_TOKEN" \
      -H "Content-Type: application/json" \
      -d "{\"otpCode\": \"$OTP2\"}" | jq .

    echo ">>> Waiting 5 seconds..."
    sleep 5

    # Check results
    echo "--- Payment Status ---"
    curl -s "$GATEWAY/api/v1/payments/$PAYMENT_ID2" \
      -H "Authorization: Bearer $USER_TOKEN" | jq '.data.status'

    echo "--- Bill Status ---"
    curl -s "$GATEWAY/api/v1/billing/bills/$BILL_ID2" \
      -H "Authorization: Bearer $USER_TOKEN" | jq '.data.status, .data.amountPaid'

    echo "--- Rewards After ---"
    curl -s "$GATEWAY/api/v1/billing/rewards/account" \
      -H "Authorization: Bearer $USER_TOKEN" | jq '.data.pointsBalance'
  fi
fi
echo ""

# ============================================
# PHASE 7: FINAL SUMMARY
# ============================================
echo ">>> PHASE 7: Final Summary"
echo "------------------------------------------"

echo "=== User Wallet ==="
curl -s "$GATEWAY/api/v1/wallets/me" -H "Authorization: Bearer $USER_TOKEN" | jq .

echo "=== User Wallet Transactions ==="
curl -s "$GATEWAY/api/v1/wallets/transactions" -H "Authorization: Bearer $USER_TOKEN" | jq '.data | length'

echo "=== User Cards ==="
curl -s "$GATEWAY/api/v1/cards" -H "Authorization: Bearer $USER_TOKEN" | jq '.data | length'

echo "=== User Payments ==="
curl -s "$GATEWAY/api/v1/payments" -H "Authorization: Bearer $USER_TOKEN" | jq '.data | length'

echo "=== Rewards Account ==="
curl -s "$GATEWAY/api/v1/billing/rewards/account" -H "Authorization: Bearer $USER_TOKEN" | jq .

echo "=========================================="
echo "TEST COMPLETE"
echo "=========================================="
echo ""
echo "Test User: $USER_EMAIL"
echo "Test User ID: $USER_ID"
echo "Card ID: $CARD_ID"
echo "Wallet ID: $WALLET_ID"
echo ""
echo "Database queries to verify:"
echo "  - OTP: SELECT OtpCode FROM Payments ORDER BY CreatedAtUtc DESC"
echo "  - Saga: SELECT * FROM PaymentOrchestrationSagas"
echo "  - Wallet: SELECT * FROM UserWallets WHERE UserId = '$USER_ID'"
echo "  - Transactions: SELECT * FROM WalletTransactions WHERE WalletId = '$WALLET_ID'"
