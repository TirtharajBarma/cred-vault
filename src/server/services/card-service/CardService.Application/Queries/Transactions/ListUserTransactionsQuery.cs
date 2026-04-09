using CardService.Application.Abstractions.Persistence;
using CardService.Domain.Entities;
using Shared.Contracts.Models;
using Shared.Contracts.DTOs.Card.Responses;
using CardService.Application.Common;
using MediatR;

namespace CardService.Application.Queries.Transactions;

public record ListUserTransactionsQuery(Guid UserId) : IRequest<ApiResponse<List<CardTransactionDto>>>;

public sealed class ListUserTransactionsQueryHandler(ICardRepository cardRepository)
    : IRequestHandler<ListUserTransactionsQuery, ApiResponse<List<CardTransactionDto>>>
{
    public async Task<ApiResponse<List<CardTransactionDto>>> Handle(ListUserTransactionsQuery request, CancellationToken cancellationToken)
    {
        var txns = await cardRepository.GetTransactionsByUserIdAsync(request.UserId, cancellationToken);

        return new ApiResponse<List<CardTransactionDto>> 
        { 
            Success = true, 
            Message = "All user transactions fetched.", 
            Data = txns.Select(CardMapping.ToDto).ToList() 
        };
    }
}
