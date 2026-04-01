namespace BillingService.Domain.Entities;

public static class BillingCycleCalculator
{
    public const int GracePeriodDays = 12;
    public const decimal MonthlyInterestRate = 0.035m;

    public static DateTime CalculateNextBillingDate(DateTime billingDate, DateTime referenceDate)
    {
        var nextBilling = new DateTime(referenceDate.Year, referenceDate.Month, billingDate.Day, 0, 0, 0, DateTimeKind.Utc);
        
        if (nextBilling <= referenceDate)
        {
            nextBilling = nextBilling.AddMonths(1);
        }
        
        if (nextBilling.Day != billingDate.Day)
        {
            nextBilling = new DateTime(nextBilling.Year, nextBilling.Month, 
                DateTime.DaysInMonth(nextBilling.Year, nextBilling.Month), 0, 0, 0, DateTimeKind.Utc);
        }
        
        return nextBilling;
    }

    public static DateTime CalculateDueDate(DateTime billingDate)
    {
        return billingDate.AddDays(GracePeriodDays);
    }

    public static DateTime GetCurrentBillingPeriodStart(int billingCycleStartDay)
    {
        var today = DateTime.UtcNow;
        var currentPeriodStart = new DateTime(today.Year, today.Month, billingCycleStartDay, 0, 0, 0, DateTimeKind.Utc);
        
        if (currentPeriodStart > today)
        {
            currentPeriodStart = currentPeriodStart.AddMonths(-1);
        }
        
        if (currentPeriodStart.Day != billingCycleStartDay && currentPeriodStart.Day != DateTime.DaysInMonth(currentPeriodStart.Year, currentPeriodStart.Month))
        {
            currentPeriodStart = new DateTime(currentPeriodStart.Year, currentPeriodStart.Month, 
                Math.Min(billingCycleStartDay, DateTime.DaysInMonth(currentPeriodStart.Year, currentPeriodStart.Month)), 
                0, 0, 0, DateTimeKind.Utc);
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
