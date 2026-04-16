-- Migration: Remove unused columns from PaymentOrchestrationSagas
-- Date: 2026-04-12
-- Reason: RiskScore and RiskDecision were dead code - never set in saga logic

USE credvault_payments;
GO

-- Check if columns exist first
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'PaymentOrchestrationSagas' AND COLUMN_NAME = 'RiskScore'
)
BEGIN
    ALTER TABLE PaymentOrchestrationSagas DROP COLUMN RiskScore;
    PRINT 'Dropped RiskScore column';
END
ELSE
    PRINT 'RiskScore column does not exist';

IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'PaymentOrchestrationSagas' AND COLUMN_NAME = 'RiskDecision'
)
BEGIN
    ALTER TABLE PaymentOrchestrationSagas DROP COLUMN RiskDecision;
    PRINT 'Dropped RiskDecision column';
END
ELSE
    PRINT 'RiskDecision column does not exist';
GO
