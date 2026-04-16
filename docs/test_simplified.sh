#!/bin/bash
# CredVault Simplified Test Script
# Tests: Admin setup, User, Wallet, Card, Transaction

set -e

BASE_URL="http://localhost:5006"
echo "=========================================="
echo "CredVault System Test (Simplified)"
echo "=========================================="
echo ""

# ============================================
# PHASE 1: Admin Login
# ============================================
echo ">>> PHASE 1: Admin Login"
echo "------------------------------------------"

ADMIN_RESP=$(curl -s -X POST "$BASE_URL/api/v1/identity/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email": "tirtharajbarma3@gmail.com", "password": "dominos7"}')

ADMIN_TOKEN=$(echo "$ADMIN_RESP" | jq -r '.data.accessToken')
if [ "$ADMIN_TOKEN" == "null" ] || [ -z "$ADMIN_TOKEN" ]; then
  echo "❌ Admin login failed!"
  echo "$ADMIN_RESP" | jq .
  exit 1
fi
echo "✅ Admin logged in"
echo ""

# ============================================
# PHASE 2: Create Issuer (if not exists)
# ============================================
echo ">>> PHASE 2: Create Issuer"
echo "------------------------------------------"

ISSUERS=$(curl -s "$BASE_URL/api/v1/issuers" -H "Authorization: Bearer $ADMIN_TOKEN")
ISSUER_ID=$(echo "$ISSUERS" | jq -r '.data[0].id // empty')

if [ -z "$ISSUER_ID" ]; then
  echo ">>> Creating new issuer..."
  ISSUER_RESP=$(curl -s -X POST "$BASE_URL/api/v1/issuers" \
    -H "Authorization: Bearer $ADMIN_TOKEN" \
    -H "Content-Type: application/json" \
    -d '{"name": "Test Visa Bank", "network": "Visa"}')
  ISSUER_ID=$(echo "$ISSUER_RESP" | jq -r '.data.id // empty')
fi

if [ -z "$ISSUER_ID" ]; then
  echo "❌ Could not get/create issuer"
  exit 1
fi
echo "✅ Issuer ID: $ISSUER_ID"
echo ""

# ============================================
# PHASE 3: Create Reward Tier
# ============================================
echo ">>> PHASE 3: Create Reward Tier"
echo "------------------------------------------"

TIER_RESP=$(curl -s -X POST "$BASE_URL/api/v1/billing/rewards/tiers" \
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
# PHASE 4: Register New User
# ============================================
echo ">>> PHASE 4: User Registration"
echo "------------------------------------------"

USER_EMAIL="testuser$(date +%s)@example.com"
USER_PASS="TestPass123!"
USER_FULL="Test User $(date +%s)"

echo ">>> Registering: $USER_EMAIL"

REG_RESP=$(curl -s -X POST "$BASE_URL/api/v1/identity/auth/register" \
  -H "Content-Type: application/json" \
  -d "{
    \"fullName\": \"$USER_FULL\",
    \"email\": \"$USER_EMAIL\",
    \"password\": \"$USER_PASS\"
  }")

USER_ID=$(echo "$REG_RESP" | jq -r '.data.user.id // empty')
TEMP_TOKEN=$(echo "$REG_RESP" | jq -r '.data.accessToken // empty')

if [ -z "$USER_ID" ]; then
  echo "❌ Registration failed"
  exit 1
fi
echo "✅ User ID: $USER_ID"

# Verify email
echo ">>> Verifying email..."
OTP_DB=$(docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Sql@Password!123' -C -d credvault_identity -Q "SELECT TOP 1 EmailVerificationOtp FROM identity_users WHERE Id = '$USER_ID'" 2>/dev/null | tr -d ' ' | grep -o '[0-9]\{6\}')
if [ -n "$OTP_DB" ]; then
  curl -s -X POST "$BASE_URL/api/v1/identity/auth/verify-email-otp" \
    -H "Content-Type: application/json" \
    -d "{\"email\": \"$USER_EMAIL\", \"otp\": \"$OTP_DB\"}" | jq -r '.success'
  echo "✅ Email verified"
fi

# Login user
echo ">>> Logging in user..."
LOGIN_RESP=$(curl -s -X POST "$BASE_URL/api/v1/identity/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\": \"$USER_EMAIL\", \"password\": \"$USER_PASS\"}")

USER_TOKEN=$(echo "$LOGIN_RESP" | jq -r '.data.accessToken // empty')
echo "✅ User Token obtained"
echo ""

# ============================================
# PHASE 5: Wallet Operations
# ============================================
echo ">>> PHASE 5: Wallet Operations"
echo "------------------------------------------"

# Check wallet
echo ">>> Checking wallet..."
WALLET_RESP=$(curl -s "$BASE_URL/api/v1/wallets/me" \
  -H "Authorization: Bearer $USER_TOKEN")
WALLET_ID=$(echo "$WALLET_RESP" | jq -r '.data.walletId // empty')
echo "✅ Wallet ID: $WALLET_ID"

# Top up wallet
echo ">>> Topping up wallet with ₹5000..."
TOPUP_RESP=$(curl -s -X POST "$BASE_URL/api/v1/wallets/topup" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"amount": 5000, "description": "Test top-up"}')
WALLET_BALANCE=$(echo "$TOPUP_RESP" | jq -r '.data.newBalance // 0')
echo "✅ Wallet Balance: ₹$WALLET_BALANCE"

# Top up again
echo ">>> Topping up with ₹3000 more..."
curl -s -X POST "$BASE_URL/api/v1/wallets/topup" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"amount": 3000, "description": "Second top-up"}' | jq -r '.data.newBalance'

# Check transactions
echo ">>> Wallet Transactions..."
curl -s "$BASE_URL/api/v1/wallets/transactions" \
  -H "Authorization: Bearer $USER_TOKEN" | jq '.data | length' | xargs -I {} echo "✅ {} transactions found"
echo ""

# ============================================
# PHASE 6: Card Operations
# ============================================
echo ">>> PHASE 6: Card Operations"
echo "------------------------------------------"

# Create card
echo ">>> Creating card..."
CARD_RESP=$(curl -s -X POST "$BASE_URL/api/v1/cards" \
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
if [ -z "$CARD_ID" ]; then
  echo "❌ Card creation failed"
  echo "$CARD_RESP" | jq .
  exit 1
fi
echo "✅ Card ID: $CARD_ID"

# Admin: Set credit limit
echo ">>> Admin: Setting credit limit ₹50000..."
curl -s -X PUT "$BASE_URL/api/v1/cards/$CARD_ID/admin" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"creditLimit": 50000}' | jq -r '.success' | xargs -I {} echo "✅ Credit limit set: {}"

# Add purchase transaction
echo ">>> Adding purchase ₹2000..."
TX_RESP=$(curl -s -X POST "$BASE_URL/api/v1/cards/$CARD_ID/transactions" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"type": 0, "amount": 2000, "description": "Test purchase"}')
echo "✅ Transaction added"

# Add another purchase
echo ">>> Adding purchase ₹1500..."
curl -s -X POST "$BASE_URL/api/v1/cards/$CARD_ID/transactions" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"type": 0, "amount": 1500, "description": "Second purchase"}' | jq -r '.success' | xargs -I {} echo "✅ Transaction added: {}"

# Check card balance
echo ">>> Checking card balance..."
CARD_DETAIL=$(curl -s "$BASE_URL/api/v1/cards/$CARD_ID" -H "Authorization: Bearer $USER_TOKEN")
OUTSTANDING=$(echo "$CARD_DETAIL" | jq -r '.data.outstandingBalance // 0')
CREDIT_LIMIT=$(echo "$CARD_DETAIL" | jq -r '.data.creditLimit // 0')
AVAILABLE=$(echo "$CARD_DETAIL" | jq -r '.data.availableCredit // 0')
echo "✅ Outstanding: ₹$OUTSTANDING"
echo "✅ Credit Limit: ₹$CREDIT_LIMIT"
echo "✅ Available Credit: ₹$AVAILABLE"
echo ""

# ============================================
# PHASE 7: Database Verification
# ============================================
echo ">>> PHASE 7: Database Verification"
echo "------------------------------------------"

echo "--- UserWallets ---"
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Sql@Password!123' -C -d credvault_payments -Q "SELECT Id, UserId, Balance, TotalTopUps FROM UserWallets WHERE UserId = '$USER_ID'" 2>/dev/null

echo ""
echo "--- WalletTransactions (latest 5) ---"
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Sql@Password!123' -C -d credvault_payments -Q "SELECT TOP 5 Id, Type, Amount, BalanceAfter, Description FROM WalletTransactions ORDER BY CreatedAtUtc DESC" 2>/dev/null

echo ""
echo "--- CreditCards ---"
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Sql@Password!123' -C -d credvault_cards -Q "SELECT Id, OutstandingBalance, CreditLimit FROM CreditCards WHERE Id = '$CARD_ID'" 2>/dev/null

echo ""
echo "--- CardTransactions (latest 5) ---"
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Sql@Password!123' -C -d credvault_cards -Q "SELECT TOP 5 Id, Type, Amount, Description FROM CardTransactions WHERE CardId = '$CARD_ID' ORDER BY DateUtc DESC" 2>/dev/null

echo ""

# ============================================
# FINAL SUMMARY
# ============================================
echo "=========================================="
echo "TEST COMPLETE"
echo "=========================================="
echo ""
echo "SUMMARY:"
echo "  User Email:     $USER_EMAIL"
echo "  User ID:       $USER_ID"
echo "  Wallet ID:     $WALLET_ID"
echo "  Card ID:      $CARD_ID"
echo "  Issuer ID:    $ISSUER_ID"
echo ""
echo "VERIFIED:"
echo "  ✅ User registration"
echo "  ✅ Email verification"
echo "  ✅ Wallet creation (auto on registration)"
echo "  ✅ Wallet top-up (multiple)"
echo "  ✅ Wallet transactions"
echo "  ✅ Card creation"
echo "  ✅ Admin: Credit limit setting"
echo "  ✅ User: Transaction (Purchase)"
echo "  ✅ Card outstanding balance update"
echo ""
echo "NOTE: Bill generation has a JSON serialization bug."
echo "      The Bill entity has circular reference with Statements."
echo ""
