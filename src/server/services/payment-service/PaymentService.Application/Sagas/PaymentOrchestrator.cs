using MassTransit;
using Microsoft.Extensions.Logging;
using PaymentService.Domain.Interfaces;
using Shared.Contracts.Events.Saga;

namespace PaymentService.Application.Sagas;

public interface IPaymentOrchestrator
{
    Task StartOrchestration(Guid correlationId, CancellationToken cancellationToken = default);
}

public class PaymentOrchestrator(
    IPublishEndpoint publishEndpoint,
    IPaymentRepository paymentRepository,
    ILogger<PaymentOrchestrator> logger
) : IPaymentOrchestrator
{
    public async Task StartOrchestration(Guid correlationId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting orchestration steps for CorrelationId={CorrelationId}", correlationId);

        var payment = await paymentRepository.GetByIdAsync(correlationId);
        if (payment == null)
        {
            logger.LogError("Payment not found: PaymentId={PaymentId}", correlationId);
            return;
        }

        await Task.Delay(100, cancellationToken);

        await publishEndpoint.Publish<IPaymentProcessRequested>(new
        {
            CorrelationId = correlationId,
            PaymentId = payment.Id,
            UserId = payment.UserId,
            Amount = payment.Amount,
            RequestedAt = DateTime.UtcNow
        }, cancellationToken);

        logger.LogInformation("Step 1: Payment process requested for CorrelationId={CorrelationId}", correlationId);
    }

    public async Task OnPaymentSucceeded(Guid correlationId, CancellationToken cancellationToken = default)
    {
        var payment = await paymentRepository.GetByIdAsync(correlationId);
        if (payment == null)
        {
            logger.LogError("Payment not found: PaymentId={PaymentId}", correlationId);
            return;
        }

        await publishEndpoint.Publish<IBillUpdateRequested>(new
        {
            CorrelationId = correlationId,
            PaymentId = payment.Id,
            UserId = payment.UserId,
            BillId = payment.BillId,
            CardId = payment.CardId,
            Amount = payment.Amount,
            RequestedAt = DateTime.UtcNow
        }, cancellationToken);

        logger.LogInformation("Step 2: Bill update requested for CorrelationId={CorrelationId}", correlationId);
    }

    public async Task OnBillUpdateSucceeded(Guid correlationId, CancellationToken cancellationToken = default)
    {
        var payment = await paymentRepository.GetByIdAsync(correlationId);
        if (payment == null)
        {
            logger.LogError("Payment not found: PaymentId={PaymentId}", correlationId);
            return;
        }

        await publishEndpoint.Publish<ICardDeductionRequested>(new
        {
            CorrelationId = correlationId,
            PaymentId = payment.Id,
            UserId = payment.UserId,
            CardId = payment.CardId,
            Amount = payment.Amount,
            RequestedAt = DateTime.UtcNow
        }, cancellationToken);

        logger.LogInformation("Step 3: Card deduction requested for CorrelationId={CorrelationId}", correlationId);
    }
}
