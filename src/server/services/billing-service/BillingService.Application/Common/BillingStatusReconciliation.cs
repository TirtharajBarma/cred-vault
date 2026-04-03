using BillingService.Domain.Entities;

namespace BillingService.Application.Common;

public static class BillingStatusReconciliation
{
    public static BillStatus ResolveBillStatus(Bill bill, DateTime utcNow)
    {
        if (bill.Status == BillStatus.Cancelled)
        {
            return BillStatus.Cancelled;
        }

        var amountPaid = Math.Max(0m, bill.AmountPaid ?? 0m);
        var outstanding = Math.Max(0m, bill.Amount - amountPaid);

        if (outstanding <= 0m)
        {
            return BillStatus.Paid;
        }

        if (bill.DueDateUtc.Date < utcNow.Date)
        {
            return BillStatus.Overdue;
        }

        if (amountPaid > 0m)
        {
            return BillStatus.PartiallyPaid;
        }

        return BillStatus.Pending;
    }

    public static StatementStatus ResolveStatementStatus(Statement statement, DateTime utcNow)
    {
        var closingBalance = Math.Max(0m, statement.ClosingBalance);
        if (closingBalance <= 0m)
        {
            return StatementStatus.Paid;
        }

        if (statement.DueDateUtc.HasValue && statement.DueDateUtc.Value.Date < utcNow.Date)
        {
            return StatementStatus.Overdue;
        }

        if (statement.AmountPaid > 0m)
        {
            return StatementStatus.PartiallyPaid;
        }

        return StatementStatus.Generated;
    }
}