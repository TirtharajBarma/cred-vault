namespace PaymentService.Application.Services;

public static class RiskCalculator
{
    /// <summary>
    /// Calculates a simple risk score for a payment based on the transaction amount.
    /// This score is used by the PaymentSaga to decide whether a payment should be auto-approved, require OTP, or be blocked.
    /// </summary>
    /// <param name="amount">The payment amount.</param>
    /// <returns>A risk score between 0 and 100.</returns>
    public static decimal Calculate(decimal amount)
    {
        // High Risk: Amount over 10,000 -> Blocked (>75 score)
        if (amount > 10000) return 85m;

        // Medium Risk: Amount over 5,000 -> OTP Required (50-74 score)
        if (amount > 5000)  return 60m;

        // Low Risk: Small amounts -> Auto Approved (<50 score)
        return 20m;
    }
}
