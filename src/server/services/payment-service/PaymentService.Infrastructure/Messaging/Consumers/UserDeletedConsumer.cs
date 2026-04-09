using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PaymentService.Infrastructure.Persistence.Sql;
using Shared.Contracts.Events.Identity;

namespace PaymentService.Infrastructure.Messaging.Consumers;

public class UserDeletedConsumer(PaymentDbContext db, ILogger<UserDeletedConsumer> logger) : IConsumer<IUserDeleted>
{
    public async Task Consume(ConsumeContext<IUserDeleted> context)
    {
        var userId = context.Message.UserId;
        logger.LogInformation("Received IUserDeleted for UserId={UserId}", userId);

        try
        {
            var payments = await db.Payments
                .Where(x => x.UserId == userId && x.Status == Domain.Enums.PaymentStatus.Initiated)
                .ToListAsync(context.CancellationToken);

            if (payments.Count == 0)
            {
                logger.LogInformation("No initiated payments for deleted user {UserId}", userId);
                return;
            }

            var now = DateTime.UtcNow;
            foreach (var payment in payments)
            {
                payment.Status = Domain.Enums.PaymentStatus.Cancelled;
                payment.UpdatedAtUtc = now;
            }

            await db.SaveChangesAsync(context.CancellationToken);
            logger.LogInformation("Cancelled {Count} initiated payments for deleted user {UserId}", payments.Count, userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process IUserDeleted for {UserId}", userId);
            throw;
        }
    }
}
