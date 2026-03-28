namespace PaymentService.Application.Services;

public static class RiskCalculator
{
    public static decimal Calculate(decimal amount)
    {
        if (amount > 10000) return 85m;
        if (amount > 5000)  return 60m;
        return 20m;
    }
}
