using MediatR;
using CardService.Application.Abstractions.Persistence;
using CardService.Application.Common;
using Shared.Contracts.Models;

namespace CardService.Application.Queries.Cards;

public record AdminGetCardByIdQuery(Guid CardId) : IRequest<ApiResponse<object>>;

public class AdminGetCardByIdQueryHandler(
    ICardRepository cardRepository) : IRequestHandler<AdminGetCardByIdQuery, ApiResponse<object>>
{
    public async Task<ApiResponse<object>> Handle(AdminGetCardByIdQuery request, CancellationToken cancellationToken)
    {
        var card = await cardRepository.GetByIdAsync(request.CardId, cancellationToken);
        if (card is null)
        {
            return new ApiResponse<object> { Success = false, Message = "Card not found." };
        }

        return new ApiResponse<object> { Success = true, Message = "Card fetched successfully.", Data = CardMapping.ToDto(card) };
    }
}
