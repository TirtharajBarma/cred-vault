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

            if (pointsToRedeem <= 0)
            {
                logger.LogInformation("Reward amount too small for redemption: Amount={Amount}", message.Amount);
                await context.Publish<IRewardRedemptionSucceeded>(new
                {
                    CorrelationId = message.CorrelationId,
                    BillId = message.BillId,
                    AmountRedeemed = 0m,
                    SucceededAt = DateTime.UtcNow
                });
                return;
            }

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
                    PaymentId = message.PaymentId,
                    BillId = message.BillId,
                    Reason = $"Billing service returned {response.StatusCode}",
                    FailedAt = DateTime.UtcNow
                });
                return;
            }

            // convert to string
            var responseBody = await response.Content.ReadAsStringAsync(context.CancellationToken);
            
            // parse string -> c# code
            var responseJson = System.Text.Json.JsonDocument.Parse(responseBody);
            var dataElement = responseJson.RootElement.GetProperty("data");             // get the "data" obj
            var actualDollarValue = dataElement.TryGetProperty("dollarValue", out var dv)       // access only the dollarValue inside the obj safety
                ? dv.GetDecimal() 
                : 0m;

            logger.LogInformation("Reward redemption successful: PaymentId={PaymentId}, BillId={BillId}, Points={Points}, ActualAmount=${Amount}",
                message.PaymentId, message.BillId, pointsToRedeem, actualDollarValue);

            await context.Publish<IRewardRedemptionSucceeded>(new
            {
                CorrelationId = message.CorrelationId,
                PaymentId = message.PaymentId,
                BillId = message.BillId,
                AmountRedeemed = actualDollarValue,
                SucceededAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process reward redemption: PaymentId={PaymentId}", message.PaymentId);

            await context.Publish<IRewardRedemptionFailed>(new
            {
                CorrelationId = message.CorrelationId,
                PaymentId = message.PaymentId,
                BillId = message.BillId,
                Reason = ex.Message,
                FailedAt = DateTime.UtcNow
            });
        }
    }
}
