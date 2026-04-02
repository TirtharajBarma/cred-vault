namespace BillingService.Domain.Entities;

public static class BillingCycleCalculator
{
    public const int GracePeriodDays = 12;
    public const decimal MonthlyInterestRate = 0.035m;

    public static DateTime CalculateDueDate(DateTime billingDate)
    {
        return billingDate.AddDays(GracePeriodDays);
    }

    public static DateTime GetCurrentBillingPeriodStart(int billingCycleStartDay)
    {
        var today = DateTime.UtcNow;
        var safeDay = Math.Min(billingCycleStartDay, DateTime.DaysInMonth(today.Year, today.Month));
        var currentPeriodStart = new DateTime(today.Year, today.Month, safeDay, 0, 0, 0, DateTimeKind.Utc);
        
        if (currentPeriodStart > today)
        {
            currentPeriodStart = currentPeriodStart.AddMonths(-1);
            safeDay = Math.Min(billingCycleStartDay, DateTime.DaysInMonth(currentPeriodStart.Year, currentPeriodStart.Month));
            currentPeriodStart = new DateTime(currentPeriodStart.Year, currentPeriodStart.Month, safeDay, 0, 0, 0, DateTimeKind.Utc);
        }
        
        return currentPeriodStart;
    }

    public static (DateTime billingDate, DateTime dueDate) CalculateBillingAndDueDate(int billingCycleStartDay)
    {
        var billingDate = GetCurrentBillingPeriodStart(billingCycleStartDay);
        var dueDate = CalculateDueDate(billingDate);
        return (billingDate, dueDate);
    }

    public static decimal CalculateMonthlyInterest(decimal overdueAmount, int overdueMonths)
    {
        if (overdueAmount <= 0 || overdueMonths <= 0)
            return 0;
        
        return Math.Round(overdueAmount * MonthlyInterestRate * overdueMonths, 2);
    }
}
