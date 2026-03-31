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
                db.RewardAccounts.Remove(rewardAccount);
            }

            await db.SaveChangesAsync(context.CancellationToken);
            logger.LogInformation("Processed IUserDeleted: cancelled {BillCount} bills for user {UserId}", bills.Count, userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process IUserDeleted for {UserId}", userId);
            throw;
        }
    }
}
