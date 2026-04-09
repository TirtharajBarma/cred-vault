using MediatR;
using CardService.Application.Abstractions.Persistence;
using Shared.Contracts.Models;
using Shared.Contracts.Exceptions;

namespace CardService.Application.Queries.Cards;

public record AdminGetCardTransactionsQuery(Guid CardId) : IRequest<ApiResponse<object>>;

public class AdminGetCardTransactionsQueryHandler(
    ICardRepository cardRepository) : IRequestHandler<AdminGetCardTransactionsQuery, ApiResponse<object>>
{
    public async Task<ApiResponse<object>> Handle(AdminGetCardTransactionsQuery request, CancellationToken cancellationToken)
    {
        var card = await cardRepository.GetByIdAsync(request.CardId, cancellationToken);
        if (card is null)
        {
            throw new NotFoundException("Card", request.CardId);
        }

        var txns = await cardRepository.GetTransactionsByCardIdAsync(request.CardId, cancellationToken);
        return new ApiResponse<object> { Success = true, Message = "Transactions fetched successfully.", Data = txns };
    }
}
