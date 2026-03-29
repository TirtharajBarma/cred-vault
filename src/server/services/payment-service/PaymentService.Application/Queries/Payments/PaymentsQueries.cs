using MediatR;
using PaymentService.Application.Common;
using PaymentService.Application.DTOs.Responses;
using PaymentService.Domain.Interfaces;

namespace PaymentService.Application.Queries.Payments;

public record GetAllPaymentsQuery(Guid UserId) : IRequest<List<PaymentDto>>;
public record GetPaymentTransactionsQuery(Guid PaymentId, Guid RequestingUserId) : IRequest<List<TransactionDto>>;
public record GetRiskScoreQuery(Guid PaymentId, Guid RequestingUserId) : IRequest<RiskScoreDto?>;

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

public class GetRiskScoreQueryHandler(
    IPaymentRepository paymentRepository,
    IRiskRepository riskRepository)
    : IRequestHandler<GetRiskScoreQuery, RiskScoreDto?>
{
    public async Task<RiskScoreDto?> Handle(GetRiskScoreQuery request, CancellationToken cancellationToken)
    {
        // Ownership check
        var payment = await paymentRepository.GetByIdAsync(request.PaymentId);
        if (payment is null || payment.UserId != request.RequestingUserId)
            return null;

        var risk = await riskRepository.GetByPaymentIdAsync(request.PaymentId);
        return risk is null ? null : PaymentMapping.ToDto(risk);
    }
}
