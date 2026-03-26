using MediatR;
using CardService.Application.DTOs.Responses;

namespace CardService.Application.Queries.Cards;

public sealed record ListIssuersQuery : IRequest<IssuersResult>;
