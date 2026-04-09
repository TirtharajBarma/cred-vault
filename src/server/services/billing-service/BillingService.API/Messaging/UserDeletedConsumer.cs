using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Events.Identity;
using BillingService.Infrastructure.Persistence.Sql;

namespace BillingService.API.Messaging;

public class UserDeletedConsumer(BillingDbContext db, ILogger<UserDeletedConsumer> logger) : IConsumer<IUserDeleted>
{
    public async Task Consume(ConsumeContext<IUserDeleted> context)
    {
        var userId = context.Message.UserId;
        logger.LogInformation("Received IUserDeleted for UserId={UserId}", userId);

        try
        {
            var bills = await db.Bills.Where(x => x.UserId == userId).ToListAsync(context.CancellationToken);
            foreach (var bill in bills)
            {
                bill.Status = Domain.Entities.BillStatus.Cancelled;
                bill.UpdatedAtUtc = DateTime.UtcNow;
            }

            var rewardAccount = await db.RewardAccounts.FirstOrDefaultAsync(x => x.UserId == userId, context.CancellationToken);
            if (rewardAccount != null)
            {
                // Delete all reward transactions for this account first (cascade handles this, but be explicit)
                var rewardTransactions = await db.RewardTransactions
                    .Where(x => x.RewardAccountId == rewardAccount.Id)
                    .ToListAsync(context.CancellationToken);
                db.RewardTransactions.RemoveRange(rewardTransactions);

                db.RewardAccounts.Remove(rewardAccount);
            }

            await db.SaveChangesAsync(context.CancellationToken);
            logger.LogInformation("Processed IUserDeleted: cancelled {BillCount} bills, removed reward account and {TxCount} transactions for user {UserId}", 
                bills.Count, rewardAccount != null ? db.RewardTransactions.Count(x => x.RewardAccountId == rewardAccount.Id) : 0, userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process IUserDeleted for {UserId}", userId);
            throw;
        }
    }
}
