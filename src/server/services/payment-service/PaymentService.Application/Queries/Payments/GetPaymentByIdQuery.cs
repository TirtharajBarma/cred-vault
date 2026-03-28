using MediatR;
using PaymentService.Application.Common;
using PaymentService.Application.DTOs.Responses;
using PaymentService.Domain.Interfaces;

namespace PaymentService.Application.Queries.Payments;

public record GetPaymentByIdQuery(Guid PaymentId, Guid RequestingUserId) : IRequest<PaymentDto?>;

public class GetPaymentByIdQueryHandler(IPaymentRepository paymentRepository)
    : IRequestHandler<GetPaymentByIdQuery, PaymentDto?>
{
    public async Task<PaymentDto?> Handle(GetPaymentByIdQuery request, CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.GetByIdAsync(request.PaymentId);

        // Only return if it belongs to the requesting user
        if (payment is null || payment.UserId != request.RequestingUserId)
            return null;

        return PaymentMapping.ToDto(payment);
    }
}
