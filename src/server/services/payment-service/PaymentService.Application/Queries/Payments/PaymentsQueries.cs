using MediatR;
using PaymentService.Application.Common;
using Shared.Contracts.DTOs.Payment.Responses;
using PaymentService.Domain.Interfaces;

namespace PaymentService.Application.Queries.Payments;

public record GetAllPaymentsQuery(Guid UserId) : IRequest<List<PaymentDto>>;
public record GetPaymentTransactionsQuery(Guid PaymentId, Guid RequestingUserId) : IRequest<List<TransactionDto>>;

public class GetAllPaymentsQueryHandler(IPaymentRepository paymentRepository)
    : IRequestHandler<GetAllPaymentsQuery, List<PaymentDto>>
{
    public async Task<List<PaymentDto>> Handle(GetAllPaymentsQuery request, CancellationToken cancellationToken)
    {
        var payments = await paymentRepository.GetByUserIdAsync(request.UserId);
        return payments.Select(PaymentMapping.ToDto).ToList();
    }
}

public class GetPaymentTransactionsQueryHandler(
    IPaymentRepository paymentRepository,
    ITransactionRepository transactionRepository)
    : IRequestHandler<GetPaymentTransactionsQuery, List<TransactionDto>>
{
    public async Task<List<TransactionDto>> Handle(GetPaymentTransactionsQuery request, CancellationToken cancellationToken)
    {
        // Ownership check
        var payment = await paymentRepository.GetByIdAsync(request.PaymentId);
        if (payment is null || payment.UserId != request.RequestingUserId)
            return [];

        var txns = await transactionRepository.GetByPaymentIdAsync(request.PaymentId);
        return txns.Select(PaymentMapping.ToDto).ToList();
    }
}
