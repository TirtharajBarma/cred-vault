using BillingService.Application.Common;
using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using MediatR;
using Shared.Contracts.Models;
using Shared.Contracts.Exceptions;

namespace BillingService.Application.Queries.Bills;

public record GetMyBillByIdQuery(Guid UserId, Guid BillId) : IRequest<ApiResponse<Bill>>;

public class GetMyBillByIdQueryHandler(IBillRepository billRepository)
    : IRequestHandler<GetMyBillByIdQuery, ApiResponse<Bill>>
{
    public async Task<ApiResponse<Bill>> Handle(GetMyBillByIdQuery request, CancellationToken cancellationToken)
    {
        var bill = await billRepository.GetByIdAndUserIdAsync(request.BillId, request.UserId, cancellationToken);

        if (bill is null)
        {
            throw new NotFoundException("Bill", request.BillId);
        }

        bill.Status = BillingStatusReconciliation.ResolveBillStatus(bill, DateTime.UtcNow);     // resolve bill status with helper function

        return new ApiResponse<Bill>
        {
            Success = true,
            Message = "Bill fetched successfully.",
            Data = bill
        };
    }
}
