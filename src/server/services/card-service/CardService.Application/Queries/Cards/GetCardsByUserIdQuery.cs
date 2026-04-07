using MediatR;
using CardService.Application.Abstractions.Persistence;
using CardService.Application.Common;
using Shared.Contracts.Models;

namespace CardService.Application.Queries.Cards;

public record GetCardsByUserIdQuery(Guid UserId) : IRequest<ApiResponse<object>>;

public class GetCardsByUserIdQueryHandler(
    ICardRepository cardRepository) : IRequestHandler<GetCardsByUserIdQuery, ApiResponse<object>>
{
    public async Task<ApiResponse<object>> Handle(GetCardsByUserIdQuery request, CancellationToken cancellationToken)
    {
        var cards = await cardRepository.ListByUserIdAsync(request.UserId, cancellationToken);
        var dtos = cards.Select(CardMapping.ToDto).ToList();
        return new ApiResponse<object> { Success = true, Message = "Cards fetched successfully.", Data = dtos };
    }
}
