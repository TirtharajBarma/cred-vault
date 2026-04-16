using MassTransit;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Services;
using Shared.Contracts.Events.Saga;
using Shared.Contracts.Events.Wallet;

namespace PaymentService.Infrastructure.Messaging.Consumers;

public sealed class WalletRefundConsumer : IConsumer<IWalletRefundRequested>
{
    private readonly IWalletService _walletService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<WalletRefundConsumer> _logger;

    public WalletRefundConsumer(
        IWalletService walletService,
        IPublishEndpoint publishEndpoint,
        ILogger<WalletRefundConsumer> logger)
    {
        _walletService = walletService;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<IWalletRefundRequested> context)
    {
        var message = context.Message;
        _logger.LogInformation(
            "Processing wallet refund for PaymentId={PaymentId}, UserId={UserId}, Amount={Amount}",
            message.PaymentId, message.UserId, message.Amount);

        try
        {
            var success = await _walletService.RefundAsync(
                message.UserId,
                message.Amount,
                message.PaymentId,
                message.Reason,
                context.CancellationToken);

            if (success)
            {
                var wallet = await _walletService.GetWalletAsync(message.UserId, context.CancellationToken);

                await _publishEndpoint.Publish<IWalletRefundSucceeded>(new
                {
                    CorrelationId = message.CorrelationId,
                    PaymentId = message.PaymentId,
                    RefundedAmount = message.Amount,
                    NewBalance = wallet?.Balance ?? 0m,
                    SucceededAt = DateTime.UtcNow
                });

                _logger.LogInformation(
                    "Wallet refund succeeded for PaymentId={PaymentId}. Refunded={Amount}, NewBalance={Balance}",
                    message.PaymentId, message.Amount, wallet?.Balance);
            }
            else
            {
                await _publishEndpoint.Publish<IWalletRefundFailed>(new
                {
                    CorrelationId = message.CorrelationId,
                    PaymentId = message.PaymentId,
                    Reason = "Wallet refund failed - wallet service returned false",
                    FailedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wallet refund failed for PaymentId={PaymentId}", message.PaymentId);

            await _publishEndpoint.Publish<IWalletRefundFailed>(new
            {
                CorrelationId = message.CorrelationId,
                PaymentId = message.PaymentId,
                Reason = ex.Message,
                FailedAt = DateTime.UtcNow
            });
        }
    }
}
