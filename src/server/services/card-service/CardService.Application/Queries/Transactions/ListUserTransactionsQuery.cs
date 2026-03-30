using CardService.Application.Abstractions.Persistence;
using CardService.Domain.Entities;
using Shared.Contracts.Models;
using MediatR;

namespace CardService.Application.Queries.Transactions;

public record ListUserTransactionsQuery(Guid UserId) : IRequest<ApiResponse<List<CardTransaction>>>;

public sealed class ListUserTransactionsQueryHandler(ICardRepository cardRepository)
    : IRequestHandler<ListUserTransactionsQuery, ApiResponse<List<CardTransaction>>>
{
    public async Task<ApiResponse<List<CardTransaction>>> Handle(ListUserTransactionsQuery request, CancellationToken cancellationToken)
    {
        var txns = await cardRepository.GetTransactionsByUserIdAsync(request.UserId, cancellationToken);

        return new ApiResponse<List<CardTransaction>> 
        { 
            Success = true, 
            Message = "All user transactions fetched.", 
            Data = txns 
        };
    }
}
