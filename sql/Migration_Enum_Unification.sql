-- ============================================================================
-- CredVault Enum Migration Script
-- Purpose: Update all enum values to be 0-based and consistent
-- Date: 2026-04-12
-- ============================================================================

-- ============================================================================
-- CLEANUP: Drop dead tables
-- ============================================================================
PRINT '=== CLEANUP: Dropping dead tables ==='

IF OBJECT_ID('credvault_payments.dbo.PaymentSagas', 'U') IS NOT NULL
BEGIN
    DROP TABLE credvault_payments.dbo.PaymentSagas;
    PRINT 'Dropped: credvault_payments.dbo.PaymentSagas (was empty)';
END

IF OBJECT_ID('credvault_billing.dbo.FraudAlerts', 'U') IS NOT NULL
BEGIN
    DROP TABLE credvault_billing.dbo.FraudAlerts;
    PRINT 'Dropped: credvault_billing.dbo.FraudAlerts';
END

IF OBJECT_ID('credvault_billing.dbo.RiskScores', 'U') IS NOT NULL
BEGIN
    DROP TABLE credvault_billing.dbo.RiskScores;
    PRINT 'Dropped: credvault_billing.dbo.RiskScores';
END

-- ============================================================================
-- 1. IDENTITY SERVICE: Update status/role to integers
-- ============================================================================
PRINT ''
PRINT '=== 1. IDENTITY SERVICE: Updating status/role to integers ==='

-- Update status from string to int
UPDATE credvault_identity.dbo.identity_users
SET status = CASE status
    WHEN 'pending-verification' THEN 0  -- PendingVerification
    WHEN 'active' THEN 1               -- Active
    WHEN 'suspended' THEN 2            -- Suspended
    WHEN 'blocked' THEN 3              -- Blocked
    WHEN 'deleted' THEN 4              -- Deleted
    ELSE 1                             -- Default to Active
END
WHERE status IS NOT NULL;

PRINT 'Updated status values: pending-verification->0, active->1, suspended->2, blocked->3, deleted->4';

-- Update role from string to int
UPDATE credvault_identity.dbo.identity_users
SET role = CASE role
    WHEN 'user' THEN 0     -- User
    WHEN 'admin' THEN 1    -- Admin
    ELSE 0                -- Default to User
END
WHERE role IS NOT NULL;

PRINT 'Updated role values: user->0, admin->1';

-- Verify identity data
PRINT ''
PRINT 'Identity Users After Migration:'
SELECT id, email, role, status FROM credvault_identity.dbo.identity_users;

-- ============================================================================
-- 2. CARD SERVICE: Update TransactionType (subtract 1 from all)
-- ============================================================================
PRINT ''
PRINT '=== 2. CARD SERVICE: Updating CardTransaction Type (subtract 1) ==='

-- Old: Purchase=1, Payment=2, Refund=3
-- New: Purchase=0, Payment=1, Refund=2

UPDATE credvault_cards.dbo.CardTransactions
SET type = type - 1
WHERE type >= 1 AND type <= 3;

PRINT 'Updated CardTransaction.Type: 1->0, 2->1, 3->2';

-- Verify card transactions
PRINT ''
PRINT 'Card Transactions After Migration:'
SELECT id, cardId, type, amount FROM credvault_cards.dbo.CardTransactions;

-- ============================================================================
-- 3. BILLING SERVICE: Update BillStatus and RewardTransactionType
-- ============================================================================
PRINT ''
PRINT '=== 3. BILLING SERVICE: Updating Bill Status (subtract 1) ==='

-- Old: Pending=1, Paid=2, Overdue=3, Cancelled=4, PartiallyPaid=5
-- New: Pending=0, Paid=1, Overdue=2, Cancelled=3, PartiallyPaid=4

UPDATE credvault_billing.dbo.Bills
SET status = status - 1
WHERE status >= 1 AND status <= 5;

PRINT 'Updated Bills.Status: 1->0, 2->1, 3->2, 4->3, 5->4';

PRINT ''
PRINT '=== 4. BILLING SERVICE: Updating RewardTransaction Type (subtract 1) ==='

-- Old: Earned=1, Adjusted=2, Redeemed=3, Reversed=4
-- New: Earned=0, Adjusted=1, Redeemed=2, Reversed=3

UPDATE credvault_billing.dbo.RewardTransactions
SET type = type - 1
WHERE type >= 1 AND type <= 4;

PRINT 'Updated RewardTransaction.Type: 1->0, 2->1, 3->2, 4->3';

-- Verify billing data
PRINT ''
PRINT 'Bills After Migration:'
SELECT id, userId, status, amount, amountPaid FROM credvault_billing.dbo.Bills;

PRINT ''
PRINT 'Reward Transactions After Migration:'
SELECT id, billId, type, points FROM credvault_billing.dbo.RewardTransactions;

-- ============================================================================
-- 5. PAYMENT SERVICE: PaymentStatus is already 0-based (no changes needed)
-- ============================================================================
PRINT ''
PRINT '=== 5. PAYMENT SERVICE: PaymentStatus already 0-based (no changes) ==='

PRINT ''
PRINT 'Payments After Migration:'
SELECT id, status, paymentType, amount FROM credvault_payments.dbo.Payments;

-- ============================================================================
-- SUMMARY
-- ============================================================================
PRINT ''
PRINT '============================================================'
PRINT 'MIGRATION COMPLETE!'
PRINT '============================================================'
PRINT ''
PRINT 'ENUM MAPPING SUMMARY:'
PRINT ''
PRINT 'IdentityService.UserStatus:'
PRINT '  pending-verification -> 0 (PendingVerification)'
PRINT '  active             -> 1 (Active)'
PRINT '  suspended          -> 2 (Suspended)'
PRINT '  blocked            -> 3 (Blocked)'
PRINT '  deleted            -> 4 (Deleted)'
PRINT ''
PRINT 'IdentityService.UserRole:'
PRINT '  user  -> 0 (User)'
PRINT '  admin -> 1 (Admin)'
PRINT ''
PRINT 'CardService.CardTransactionType:'
PRINT '  1 -> 0 (Purchase)'
PRINT '  2 -> 1 (Payment)'
PRINT '  3 -> 2 (Refund)'
PRINT ''
PRINT 'BillingService.BillStatus:'
PRINT '  1 -> 0 (Pending)'
PRINT '  2 -> 1 (Paid)'
PRINT '  3 -> 2 (Overdue)'
PRINT '  4 -> 3 (Cancelled)'
PRINT '  5 -> 4 (PartiallyPaid)'
PRINT ''
PRINT 'BillingService.RewardTransactionType:'
PRINT '  1 -> 0 (Earned)'
PRINT '  2 -> 1 (Adjusted)'
PRINT '  3 -> 2 (Redeemed)'
PRINT '  4 -> 3 (Reversed)'
PRINT ''
PRINT 'PaymentService.PaymentStatus: Already 0-based (no changes)'
PRINT '  0=Initiated, 1=Processing, 2=Completed, 3=Failed,'
PRINT '  4=Reversed, 5=Cancelled, 6=Expired'
PRINT ''
PRINT '============================================================'
GO
