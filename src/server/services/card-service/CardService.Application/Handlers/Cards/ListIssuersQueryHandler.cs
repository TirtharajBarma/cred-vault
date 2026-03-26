using CardService.Application.Abstractions.Persistence;
using CardService.Application.DTOs.Responses;
using MediatR;

namespace CardService.Application.Queries.Cards;

public sealed class ListIssuersQueryHandler(ICardRepository cardRepository) 
    : IRequestHandler<ListIssuersQuery, IssuersResult>
{
    public async Task<IssuersResult> Handle(ListIssuersQuery request, CancellationToken cancellationToken)
    {
        var issuers = await cardRepository.ListIssuersAsync(cancellationToken);

        var dtoIssuers = issuers.Select(x => new IssuerDto
        {
            Id = x.Id,
            Name = x.Name,
            Network = x.Network.ToString()
        }).ToList();

        return new IssuersResult
        {
            Success = true,
            Issuers = dtoIssuers,
            Message = "Issuers fetched successfully."
        };
    }
}
