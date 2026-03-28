using CardService.Application.Abstractions.Persistence;
using CardService.Application.DTOs.Responses;
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
            Issuers = issuers.Select(i => new IssuerDto
            {
                Id = i.Id,
                Name = i.Name,
                Network = i.Network.ToString()
            }).ToList()
        };
    }
}
