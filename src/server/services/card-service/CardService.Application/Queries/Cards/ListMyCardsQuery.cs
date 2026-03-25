using CardService.Application.DTOs.Responses;
using MediatR;

namespace CardService.Application.Queries.Cards;

public sealed record ListMyCardsQuery(Guid UserId) : IRequest<CardsResult>;
