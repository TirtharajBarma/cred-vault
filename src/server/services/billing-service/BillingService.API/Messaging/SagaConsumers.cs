using MassTransit;
using Microsoft.Extensions.Logging;
using MediatR;
using BillingService.Application.Commands.Bills;
using Shared.Contracts.Events.Saga;

namespace BillingService.API.Messaging;

public class BillUpdateSagaConsumer(
    IMediator mediator,
    ILogger<BillUpdateSagaConsumer> logger
) : IConsumer<IBillUpdateRequested>
{
    public async Task Consume(ConsumeContext<IBillUpdateRequested> context)
    {
        var message = context.Message;

        logger.LogInformation("BillUpdateSagaConsumer: CorrelationId={CorrelationId}, BillId={BillId}, Amount={Amount}",
            message.CorrelationId, message.BillId, message.Amount);

        try
        {
            var result = await mediator.Send(new MarkBillPaidCommand(
                message.UserId,
                message.BillId,
                message.Amount
            ), context.CancellationToken);

            if (result.Success)
            {
                logger.LogInformation("Bill update succeeded: CorrelationId={CorrelationId}, BillId={BillId}",
                    message.CorrelationId, message.BillId);

                await context.Publish<IBillUpdateSucceeded>(new
                {
                    CorrelationId = message.CorrelationId,
                    BillId = message.BillId,
                    SucceededAt = DateTime.UtcNow
                });
            }
            else
            {
                logger.LogWarning("Bill update failed: CorrelationId={CorrelationId}, BillId={BillId}, Reason={Reason}",
                    message.CorrelationId, message.BillId, result.Message);

                await context.Publish<IBillUpdateFailed>(new
                {
                    CorrelationId = message.CorrelationId,
                    BillId = message.BillId,
                    Reason = result.Message ?? "Unknown error",
                    FailedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bill update exception: CorrelationId={CorrelationId}, BillId={BillId}",
                message.CorrelationId, message.BillId);

            await context.Publish<IBillUpdateFailed>(new
            {
                CorrelationId = message.CorrelationId,
                BillId = message.BillId,
                Reason = ex.Message,
                FailedAt = DateTime.UtcNow
            });
        }
    }
}

public class RevertBillSagaConsumer(
    IMediator mediator,
    ILogger<RevertBillSagaConsumer> logger
) : IConsumer<IRevertBillUpdateRequested>
{
    public async Task Consume(ConsumeContext<IRevertBillUpdateRequested> context)
    {
        var message = context.Message;

        logger.LogInformation("RevertBillSagaConsumer: CorrelationId={CorrelationId}, BillId={BillId}, Amount={Amount}",
            message.CorrelationId, message.BillId, message.Amount);

        try
        {
            var result = await mediator.Send(new RevertBillPaidCommand(
                message.UserId,
                message.BillId,
                message.Amount
            ), context.CancellationToken);

            if (result.Success)
            {
                logger.LogInformation("Bill revert succeeded: CorrelationId={CorrelationId}, BillId={BillId}",
                    message.CorrelationId, message.BillId);

                await context.Publish<IRevertBillUpdateSucceeded>(new
                {
                    CorrelationId = message.CorrelationId,
                    BillId = message.BillId,
                    SucceededAt = DateTime.UtcNow
                });
            }
            else
            {
                logger.LogWarning("Bill revert failed: CorrelationId={CorrelationId}, BillId={BillId}, Reason={Reason}",
                    message.CorrelationId, message.BillId, result.Message);

                await context.Publish<IRevertBillUpdateFailed>(new
                {
                    CorrelationId = message.CorrelationId,
                    BillId = message.BillId,
                    Reason = result.Message ?? "Unknown error",
                    FailedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bill revert exception: CorrelationId={CorrelationId}, BillId={BillId}",
                message.CorrelationId, message.BillId);

            await context.Publish<IRevertBillUpdateFailed>(new
            {
                CorrelationId = message.CorrelationId,
                BillId = message.BillId,
                Reason = ex.Message,
                FailedAt = DateTime.UtcNow
            });
        }
    }
}
