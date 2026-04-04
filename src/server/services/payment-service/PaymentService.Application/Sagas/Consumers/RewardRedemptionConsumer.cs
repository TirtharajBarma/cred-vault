using System.Net.Http.Json;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Events.Saga;

namespace PaymentService.Application.Sagas.Consumers;

public class RewardRedemptionConsumer(
    IHttpClientFactory httpClientFactory,
    Microsoft.Extensions.Configuration.IConfiguration configuration,
    ILogger<RewardRedemptionConsumer> logger
) : IConsumer<IRewardRedemptionRequested>
{
    public async Task Consume(ConsumeContext<IRewardRedemptionRequested> context)
    {
        var message = context.Message;

        logger.LogInformation("Processing reward redemption: PaymentId={PaymentId}, BillId={BillId}, Amount={Amount}",
            message.PaymentId, message.BillId, message.Amount);

        try
        {
            var pointsToRedeem = (int)Math.Floor(message.Amount / 0.25m);
            pointsToRedeem = Math.Max(pointsToRedeem, 1);

            var client = httpClientFactory.CreateClient();
            var billingUrl = configuration["Services:BillingService"] ?? "http://localhost:5003";
            client.BaseAddress = new Uri(billingUrl);

            // Use internal endpoint without auth requirement
            var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/billing/rewards/internal/redeem")
            {
                Content = JsonContent.Create(new
                {
                    UserId = message.UserId,
                    Points = pointsToRedeem,
                    Target = "Bill",
                    BillId = message.BillId
                })
            };

            var response = await client.SendAsync(request, context.CancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(context.CancellationToken);
                logger.LogWarning("Reward redemption failed: {Status} - {Content}", response.StatusCode, errorContent);
                
                await context.Publish<IRewardRedemptionFailed>(new
                {
                    CorrelationId = message.CorrelationId,
                    BillId = message.BillId,
                    Reason = $"Billing service returned {response.StatusCode}",
                    FailedAt = DateTime.UtcNow
                });
                return;
            }

            logger.LogInformation("Reward redemption successful: PaymentId={PaymentId}, BillId={BillId}, Points={Points}",
                message.PaymentId, message.BillId, pointsToRedeem);

            await context.Publish<IRewardRedemptionSucceeded>(new
            {
                CorrelationId = message.CorrelationId,
                BillId = message.BillId,
                AmountRedeemed = message.Amount,
                SucceededAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process reward redemption: PaymentId={PaymentId}", message.PaymentId);

            await context.Publish<IRewardRedemptionFailed>(new
            {
                CorrelationId = message.CorrelationId,
                BillId = message.BillId,
                Reason = ex.Message,
                FailedAt = DateTime.UtcNow
            });
        }
    }
}
