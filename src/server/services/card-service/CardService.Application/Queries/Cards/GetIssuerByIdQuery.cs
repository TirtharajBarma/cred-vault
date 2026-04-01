using MediatR;
using CardService.Application.Abstractions.Persistence;
using CardService.Domain.Entities;
using Shared.Contracts.DTOs.Card.Responses;

namespace CardService.Application.Queries.Cards;

public sealed record GetIssuerByIdQuery(Guid Id) : IRequest<CardIssuerDto>;

public sealed class GetIssuerByIdQueryHandler(ICardRepository cardRepository) : IRequestHandler<GetIssuerByIdQuery, CardIssuerDto>
{
    public async Task<CardIssuerDto> Handle(GetIssuerByIdQuery request, CancellationToken cancellationToken)
    {
        var issuer = await cardRepository.GetIssuerByIdAsync(request.Id, cancellationToken);
        
        if (issuer == null)
        {
            return new CardIssuerDto();
        }

        return new CardIssuerDto
        {
            Id = issuer.Id,
            Name = issuer.Name,
            Network = issuer.Network.ToString(),
            CreatedAtUtc = issuer.CreatedAtUtc,
            UpdatedAtUtc = issuer.UpdatedAtUtc
        };
    }
}