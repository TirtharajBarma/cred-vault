using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Events.Identity;
using CardService.Infrastructure.Persistence.Sql;

namespace CardService.API.Messaging;

public class UserDeletedConsumer(CardDbContext db, ILogger<UserDeletedConsumer> logger) : IConsumer<IUserDeleted>
{
    public async Task Consume(ConsumeContext<IUserDeleted> context)
    {
        var userId = context.Message.UserId;
        logger.LogInformation("Received IUserDeleted for UserId={UserId}", userId);

        try
        {
            var cards = await db.CreditCards
                .Where(x => x.UserId == userId)         // find all cards of that user
                .ToListAsync(context.CancellationToken);

            if (cards.Count == 0)
            {
                logger.LogInformation("No cards found for deleted user {UserId}", userId);
                return;
            }

            var now = DateTime.UtcNow;
            foreach (var card in cards)         // soft-delete the card
            {
                card.IsDeleted = true;
                card.DeletedAtUtc = now;
                card.UpdatedAtUtc = now;
                card.IsDefault = false;
            }

            await db.SaveChangesAsync(context.CancellationToken);
            logger.LogInformation("Soft-deleted {Count} cards for deleted user {UserId}", cards.Count, userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process IUserDeleted for {UserId}", userId);
            throw;
        }
    }
}
