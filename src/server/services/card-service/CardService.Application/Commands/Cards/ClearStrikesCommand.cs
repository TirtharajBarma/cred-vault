using CardService.Application.Abstractions.Persistence;
using CardService.Domain.Entities;
using Shared.Contracts.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CardService.Application.Commands.Cards;

public record ClearStrikesCommand(Guid CardId) : IRequest<ApiResponse<bool>>;

public class ClearStrikesCommandHandler(ICardRepository cards, IViolationRepository violations, ILogger<ClearStrikesCommandHandler> logger) : IRequestHandler<ClearStrikesCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ClearStrikesCommand request, CancellationToken ct)
    {
        logger.LogInformation("ClearStrikes requested: CardId={CardId}", request.CardId);

        var card = await cards.GetByIdAsync(request.CardId, ct);
        if (card == null)
        {
            logger.LogWarning("Card not found: {CardId}", request.CardId);
            return new() { Success = false, Message = "Card not found" };
        }

        var activeViolations = await violations.GetViolationsByCardIdAsync(request.CardId, ct);
        foreach (var violation in activeViolations.Where(v => v.IsActive))
        {
            violation.IsActive = false;
            violation.ClearedAtUtc = DateTime.UtcNow;
            await violations.UpdateAsync(violation, ct);
        }

        card.ClearStrikes();
        card.UpdatedAtUtc = DateTime.UtcNow;
        await cards.UpdateAsync(card, ct);

        logger.LogInformation("Strikes cleared for Card {CardId}", request.CardId);

        return new() { Success = true, Message = "Strikes cleared", Data = true };
    }
}
