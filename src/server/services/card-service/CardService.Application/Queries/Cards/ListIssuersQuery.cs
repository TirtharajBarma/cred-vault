using CardService.Application.Abstractions.Persistence;
using Shared.Contracts.DTOs.Card.Responses;
using MediatR;

namespace CardService.Application.Queries.Cards;

public sealed record ListIssuersQuery : IRequest<IssuersResult>;

public sealed class ListIssuersQueryHandler(ICardRepository cardRepository)
    : IRequestHandler<ListIssuersQuery, IssuersResult>
{
    public async Task<IssuersResult> Handle(ListIssuersQuery request, CancellationToken cancellationToken)
    {
        var issuers = await cardRepository.ListIssuersAsync(cancellationToken);

        return new IssuersResult
        {
            Success = true,
            Message = "Issuers fetched successfully.",
            Issuers = issuers.Select(i => new CardIssuerDto
            {
                Id = i.Id,
                Name = i.Name,
                Network = i.Network.ToString(),
                CreatedAtUtc = i.CreatedAtUtc,
                UpdatedAtUtc = i.UpdatedAtUtc
            }).ToList()
        };
    }
}
