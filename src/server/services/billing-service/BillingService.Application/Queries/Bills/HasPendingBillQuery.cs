using MediatR;
using BillingService.Application.Abstractions.Persistence;

namespace BillingService.Application.Queries.Bills;

public record HasPendingBillQuery(Guid CardId) : IRequest<HasPendingBillResponse>;

public class HasPendingBillQueryHandler(IBillRepository billRepository) 
    : IRequestHandler<HasPendingBillQuery, HasPendingBillResponse>
{
    public async Task<HasPendingBillResponse> Handle(HasPendingBillQuery request, CancellationToken cancellationToken)
    {
        var hasPending = await billRepository.HasPendingBillForCardAsync(request.CardId, cancellationToken);
        return new HasPendingBillResponse { HasPendingBill = hasPending };
    }
}

public class HasPendingBillResponse
{
    public bool HasPendingBill { get; set; }
}
