using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using MediatR;
using Shared.Contracts.Models;

namespace BillingService.Application.Queries.Bills;

public record GetMyBillsQuery(Guid UserId) : IRequest<ApiResponse<List<Bill>>>;

public class GetMyBillsQueryHandler(IBillRepository billRepository)
    : IRequestHandler<GetMyBillsQuery, ApiResponse<List<Bill>>>
{
    public async Task<ApiResponse<List<Bill>>> Handle(GetMyBillsQuery request, CancellationToken cancellationToken)
    {
        var bills = await billRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        return new ApiResponse<List<Bill>>
        {
            Success = true,
            Message = "Bills fetched successfully.",
            Data = bills
        };
    }
}
