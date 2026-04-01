-- Correlation audit for payment -> saga -> bill -> card transaction -> notifications
-- Usage:
--   1) Run for all recent payments: keep @PaymentId = NULL
--   2) Run for one payment: set @PaymentId to a specific ID

DECLARE @PaymentId UNIQUEIDENTIFIER = NULL;

WITH base AS (
    SELECT
        p.Id AS PaymentId,
        p.UserId,
        p.CardId,
        p.BillId,
        p.Amount,
        p.Status AS PaymentStatus,
        p.CreatedAtUtc,
        p.UpdatedAtUtc,
        s.CurrentState AS SagaState,
        s.PaymentProcessed,
        s.BillUpdated,
        s.CardDeducted,
        s.OtpVerified,
        s.Email AS SagaEmail,
        s.FullName AS SagaFullName,
        b.Status AS BillStatus,
        b.AmountPaid AS BillAmountPaid,
        b.PaidAtUtc AS BillPaidAtUtc
    FROM credvault_payments.dbo.Payments p
    LEFT JOIN credvault_payments.dbo.PaymentOrchestrationSagas s
        ON s.CorrelationId = p.Id
    LEFT JOIN credvault_billing.dbo.Bills b
        ON b.Id = p.BillId
    WHERE (@PaymentId IS NULL OR p.Id = @PaymentId)
),
pay_txn AS (
    SELECT
        t.PaymentId,
        COUNT(*) AS PaymentTxnCount,
        MAX(t.CreatedAtUtc) AS LastPaymentTxnAt
    FROM credvault_payments.dbo.Transactions t
    GROUP BY t.PaymentId
),
card_txn AS (
    SELECT
        p.Id AS PaymentId,
        COUNT(*) AS CardTxnCount,
        MAX(ct.DateUtc) AS LastCardTxnAt
    FROM credvault_payments.dbo.Payments p
    LEFT JOIN credvault_cards.dbo.CardTransactions ct
        ON ct.CardId = p.CardId
       AND ct.[Description] LIKE CONCAT('Bill Payment:', CAST(p.Id AS VARCHAR(36)), '%')
    WHERE (@PaymentId IS NULL OR p.Id = @PaymentId)
    GROUP BY p.Id
),
notif AS (
    SELECT
        p.Id AS PaymentId,
        SUM(CASE WHEN nl.Subject IN ('Payment Verification', 'PaymentOtpGenerated') THEN 1 ELSE 0 END) AS OtpNotificationCount,
        SUM(CASE WHEN nl.Subject IN ('Payment Successful', 'PaymentCompleted') THEN 1 ELSE 0 END) AS CompletionNotificationCount,
        SUM(CASE WHEN nl.IsSuccess = 1 THEN 1 ELSE 0 END) AS NotificationSuccessCount,
        SUM(CASE WHEN nl.IsSuccess = 0 THEN 1 ELSE 0 END) AS NotificationFailureCount,
        MAX(nl.CreatedAtUtc) AS LastNotificationAt
    FROM credvault_payments.dbo.Payments p
    LEFT JOIN credvault_notifications.dbo.NotificationLogs nl
        ON nl.Body LIKE CONCAT('%"PaymentId":"', CAST(p.Id AS VARCHAR(36)), '"%')
    WHERE (@PaymentId IS NULL OR p.Id = @PaymentId)
    GROUP BY p.Id
)
SELECT
    b.PaymentId,
    b.UserId,
    b.CardId,
    b.BillId,
    b.Amount,
    b.PaymentStatus,
    b.SagaState,
    b.PaymentProcessed,
    b.BillUpdated,
    b.CardDeducted,
    b.OtpVerified,
    b.SagaEmail,
    b.SagaFullName,
    b.BillStatus,
    b.BillAmountPaid,
    b.BillPaidAtUtc,
    ISNULL(pt.PaymentTxnCount, 0) AS PaymentTxnCount,
    pt.LastPaymentTxnAt,
    ISNULL(ct.CardTxnCount, 0) AS CardTxnCount,
    ct.LastCardTxnAt,
    ISNULL(n.OtpNotificationCount, 0) AS OtpNotificationCount,
    ISNULL(n.CompletionNotificationCount, 0) AS CompletionNotificationCount,
    ISNULL(n.NotificationSuccessCount, 0) AS NotificationSuccessCount,
    ISNULL(n.NotificationFailureCount, 0) AS NotificationFailureCount,
    n.LastNotificationAt,
    CASE WHEN b.SagaState = 'Completed' THEN 1 ELSE 0 END AS IsSagaCompleted,
    CASE WHEN ISNULL(pt.PaymentTxnCount, 0) > 0 THEN 1 ELSE 0 END AS HasPaymentTxn,
    CASE WHEN ISNULL(ct.CardTxnCount, 0) > 0 THEN 1 ELSE 0 END AS HasCardTxn,
    CASE WHEN ISNULL(n.CompletionNotificationCount, 0) > 0 THEN 1 ELSE 0 END AS HasCompletionNotification
FROM base b
LEFT JOIN pay_txn pt ON pt.PaymentId = b.PaymentId
LEFT JOIN card_txn ct ON ct.PaymentId = b.PaymentId
LEFT JOIN notif n ON n.PaymentId = b.PaymentId
ORDER BY b.CreatedAtUtc DESC;
