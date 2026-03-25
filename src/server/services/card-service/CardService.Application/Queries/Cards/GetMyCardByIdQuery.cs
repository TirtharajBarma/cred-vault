using CardService.Application.DTOs.Responses;
using MediatR;

namespace CardService.Application.Queries.Cards;

public sealed record GetMyCardByIdQuery(Guid UserId, Guid CardId) : IRequest<CardResult>;
