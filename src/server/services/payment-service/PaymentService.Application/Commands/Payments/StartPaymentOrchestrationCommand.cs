using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using PaymentService.Domain.Interfaces;
using Shared.Contracts.Events.Saga;

namespace PaymentService.Application.Commands.Payments;

public record StartPaymentOrchestrationCommand(
    Guid PaymentId,
    Guid UserId,
    string Email,
    string FullName,
    Guid CardId,
    Guid BillId,
    decimal Amount,
    string PaymentType,
    decimal RiskScore
) : IRequest<bool>;

public class StartPaymentOrchestrationCommandHandler(
    IPublishEndpoint publishEndpoint,
    ILogger<StartPaymentOrchestrationCommandHandler> logger
) : IRequestHandler<StartPaymentOrchestrationCommand, bool>
{
    public async Task<bool> Handle(StartPaymentOrchestrationCommand request, CancellationToken cancellationToken)
    {
        var correlationId = request.PaymentId;

        logger.LogInformation("Starting payment orchestration: PaymentId={PaymentId}, CorrelationId={CorrelationId}",
            request.PaymentId, correlationId);

        try
        {
            await publishEndpoint.Publish<IStartPaymentOrchestration>(new
            {
                CorrelationId = correlationId,
                request.PaymentId,
                request.UserId,
                request.Email,
                request.FullName,
                request.CardId,
                request.BillId,
                request.Amount,
                request.PaymentType,
                request.RiskScore,
                StartedAt = DateTime.UtcNow
            }, cancellationToken);

            logger.LogInformation("Payment orchestration started: CorrelationId={CorrelationId}", correlationId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start payment orchestration: PaymentId={PaymentId}", request.PaymentId);
            return false;
        }
    }
}
