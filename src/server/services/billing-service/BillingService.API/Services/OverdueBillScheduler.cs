using BillingService.Application.Commands.Bills;
using MediatR;

namespace BillingService.API.Services;

public sealed class OverdueBillScheduler(
    IServiceScopeFactory scopeFactory,
    ILogger<OverdueBillScheduler> logger
) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit for app startup and dependencies.
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var result = await mediator.Send(new CheckOverdueBillsCommand(), stoppingToken);

                logger.LogInformation(
                    "OverdueBillScheduler run completed: Success={Success}, Message={Message}",
                    result.Success,
                    result.Message);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OverdueBillScheduler failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
