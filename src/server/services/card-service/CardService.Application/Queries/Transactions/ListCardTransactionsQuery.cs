using CardService.Application.Abstractions.Persistence;
using CardService.Domain.Entities;
using Shared.Contracts.Models;
using MediatR;

namespace CardService.Application.Queries.Transactions;

public record ListCardTransactionsQuery(Guid UserId, Guid CardId) : IRequest<ApiResponse<List<CardTransaction>>>;

public sealed class ListCardTransactionsQueryHandler(ICardRepository cardRepository)
    : IRequestHandler<ListCardTransactionsQuery, ApiResponse<List<CardTransaction>>>
{
    public async Task<ApiResponse<List<CardTransaction>>> Handle(ListCardTransactionsQuery request, CancellationToken cancellationToken)
    {
        var txns = await cardRepository.GetTransactionsByCardAndUserAsync(request.CardId, request.UserId, cancellationToken);

        return new ApiResponse<List<CardTransaction>> 
        { 
            Success = true, 
            Message = "Transactions fetched.", 
            Data = txns 
        };
    }
}
