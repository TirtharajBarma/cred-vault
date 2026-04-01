using CardService.Application.Abstractions.Persistence;
using Shared.Contracts.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CardService.Application.Commands.Cards;

public record UnblockCardCommand(Guid CardId) : IRequest<ApiResponse<bool>>;

public class UnblockCardCommandHandler(ICardRepository cards, ILogger<UnblockCardCommandHandler> logger) : IRequestHandler<UnblockCardCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UnblockCardCommand request, CancellationToken ct)
    {
        logger.LogInformation("UnblockCardCommand: CardId={CardId}", request.CardId);

        var card = await cards.GetByIdAsync(request.CardId, ct);
        if (card == null)
        {
            return new() { Success = false, Message = "Card not found" };
        }

        if (!card.IsBlocked)
        {
            return new() { Success = false, Message = "Card is not blocked" };
        }

        card.StrikeCount = 0;
        card.IsBlocked = false;
        card.UnblockedAtUtc = DateTime.UtcNow;
        card.UpdatedAtUtc = DateTime.UtcNow;

        await cards.UpdateAsync(card, ct);

        logger.LogInformation("Card unblocked: CardId={CardId}", request.CardId);

        return new() { Success = true, Message = "Card unblocked", Data = true };
    }
}
