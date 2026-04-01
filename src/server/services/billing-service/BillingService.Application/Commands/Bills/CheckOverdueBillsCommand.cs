using BillingService.Application.Abstractions.Persistence;
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
            if (bill.DueDateUtc < DateTime.UtcNow && bill.Status == BillStatus.Pending)
            {
                bill.Status = BillStatus.Overdue;
                bill.UpdatedAtUtc = DateTime.UtcNow;
                await bills.UpdateAsync(bill, ct);

                await publisher.Publish<IBillOverdueDetected>(new
                {
                    BillId = bill.Id,
                    CardId = bill.CardId,
                    UserId = bill.UserId,
                    OverdueAmount = bill.Amount,
                    DueDate = bill.DueDateUtc,
                    DaysOverdue = (int)(DateTime.UtcNow - bill.DueDateUtc).TotalDays,
                    DetectedAt = DateTime.UtcNow
                }, ct);

                overdueCount++;
                logger.LogInformation("Bill marked overdue: BillId={BillId}, DaysOverdue={Days}", 
                    bill.Id, (DateTime.UtcNow - bill.DueDateUtc).TotalDays);
            }
        }

        if (overdueCount > 0)
        {
            await unitOfWork.SaveChangesAsync(ct);
        }

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
