using BillingService.Application.Abstractions.Persistence;
using BillingService.Application.Common;
using BillingService.Domain.Entities;
using Shared.Contracts.Models;
using Shared.Contracts.Events.Saga;
using MediatR;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace BillingService.Application.Commands.Bills;

public record CheckOverdueBillsCommand : IRequest<ApiResponse<OverdueCheckResult>>;

public record OverdueCheckResult(int BillsChecked, int OverdueCount);

public class CheckOverdueBillsCommandHandler(
    IBillRepository bills,
    IStatementRepository statements,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publisher,
    ILogger<CheckOverdueBillsCommandHandler> logger
) : IRequestHandler<CheckOverdueBillsCommand, ApiResponse<OverdueCheckResult>>
{
    public async Task<ApiResponse<OverdueCheckResult>> Handle(CheckOverdueBillsCommand request, CancellationToken ct)
    {
        logger.LogInformation("CheckOverdueBillsCommand triggered");

        var pendingBills = await GetPendingBillsAsync(ct);
        var overdueCount = 0;

        foreach (var bill in pendingBills)
        {
            if (bill.DueDateUtc < DateTime.UtcNow)
            {
                var outstandingAmount = Math.Max(0m, bill.Amount - (bill.AmountPaid ?? 0m));
                if (outstandingAmount <= 0m)
                {
                    continue;
                }

                var normalizedStatus = BillingStatusReconciliation.ResolveBillStatus(bill, DateTime.UtcNow);
                if (normalizedStatus != BillStatus.Overdue)
                {
                    normalizedStatus = BillStatus.Overdue;
                }

                if (bill.Status != normalizedStatus)
                {
                    bill.Status = normalizedStatus;
                    bill.UpdatedAtUtc = DateTime.UtcNow;
                    await bills.UpdateAsync(bill, ct);
                }

                var statement = await statements.GetByBillIdAsync(bill.Id, ct);
                if (statement != null)
                {
                    statement.AmountPaid = bill.AmountPaid ?? 0m;
                    statement.TotalPayments = bill.AmountPaid ?? 0m;
                    statement.ClosingBalance = outstandingAmount;
                    statement.Status = StatementStatus.Overdue;
                    statement.DueDateUtc = bill.DueDateUtc;
                    statement.UpdatedAtUtc = DateTime.UtcNow;
                    await statements.UpdateAsync(statement, ct);
                }

                var daysOverdue = Math.Max(1, (int)(DateTime.UtcNow.Date - bill.DueDateUtc.Date).TotalDays);

                await publisher.Publish<IBillOverdueDetected>(new
                {
                    BillId = bill.Id,
                    CardId = bill.CardId,
                    UserId = bill.UserId,
                    OverdueAmount = outstandingAmount,
                    DueDate = bill.DueDateUtc,
                    DaysOverdue = daysOverdue,
                    DetectedAt = DateTime.UtcNow
                }, ct);

                overdueCount++;
                logger.LogInformation("Bill marked overdue: BillId={BillId}, DaysOverdue={Days}", 
                    bill.Id, daysOverdue);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Overdue check completed: Checked={Checked}, Overdue={Overdue}", pendingBills.Count, overdueCount);

        return new()
        {
            Success = true,
            Message = $"Checked {pendingBills.Count} bills, {overdueCount} marked overdue",
            Data = new OverdueCheckResult(pendingBills.Count, overdueCount)
        };
    }

    private Task<List<Bill>> GetPendingBillsAsync(CancellationToken ct)
    {
        return bills.GetOverdueBillsAsync(ct);
    }
}
