using MediatR;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Interfaces;
using Shared.Contracts.Events.Payment;
using MassTransit;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Commands.Payments;

public record ReversePaymentCommand(Guid PaymentId, Guid RequestingUserId) : IRequest<bool>;

public class ReversePaymentCommandHandler(
    IPaymentRepository paymentRepository,
    ITransactionRepository transactionRepository,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publishEndpoint) : IRequestHandler<ReversePaymentCommand, bool>
{
    public async Task<bool> Handle(ReversePaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.GetByIdAsync(request.PaymentId);

        // Ownership check
        if (payment is null || payment.UserId != request.RequestingUserId)
            return false;

        payment.Status = PaymentStatus.Reversed;
        payment.UpdatedAtUtc = DateTime.UtcNow;

        await paymentRepository.UpdateAsync(payment);

        // Insert Reversal Transaction
        await transactionRepository.AddAsync(new Transaction
        {
            Id = Guid.NewGuid(),
            PaymentId = payment.Id,
            UserId = payment.UserId,
            Amount = payment.Amount,
            Type = TransactionType.Reversal,
            Description = "Manual payment reversal",
            CreatedAtUtc = DateTime.UtcNow
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Publish event
        await publishEndpoint.Publish<IPaymentFailed>(new
        {
            PaymentId = payment.Id,
            UserId = payment.UserId,
            Reason = "Payment Reversed",
            FailedAt = DateTime.UtcNow
        }, cancellationToken);

        return true;
    }
}
