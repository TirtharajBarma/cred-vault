using CardService.Application.Abstractions.Persistence;
using CardService.Domain.Entities;
using Shared.Contracts.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CardService.Application.Queries.Cards;

public record GetBlockedCardsQuery : IRequest<ApiResponse<List<BlockedCardDto>>>;

public record BlockedCardDto(
    Guid CardId,
    Guid UserId,
    string Last4,
    int StrikeCount,
    decimal OutstandingBalance,
    decimal CreditLimit,
    DateTime? BlockedAtUtc
);

public class GetBlockedCardsQueryHandler(ICardRepository cards, ILogger<GetBlockedCardsQueryHandler> logger) : IRequestHandler<GetBlockedCardsQuery, ApiResponse<List<BlockedCardDto>>>
{
    public async Task<ApiResponse<List<BlockedCardDto>>> Handle(GetBlockedCardsQuery request, CancellationToken ct)
    {
        logger.LogInformation("GetBlockedCardsQuery");

        var blockedCards = await cards.GetBlockedCardsAsync(ct);

        var result = blockedCards.Select(c => new BlockedCardDto(
            c.Id,
            c.UserId,
            c.Last4,
            c.StrikeCount,
            c.OutstandingBalance,
            c.CreditLimit,
            c.BlockedAtUtc
        )).ToList();

        return new() { Success = true, Data = result };
    }
}
