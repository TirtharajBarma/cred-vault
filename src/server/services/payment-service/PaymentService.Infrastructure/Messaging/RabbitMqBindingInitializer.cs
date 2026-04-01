using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace PaymentService.Infrastructure.Messaging;

public class RabbitMqBindingInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMqBindingInitializer> _logger;
    private IConnection? _connection;

    public RabbitMqBindingInitializer(IServiceProvider serviceProvider, ILogger<RabbitMqBindingInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest"
            };

            _connection = factory.CreateConnection();
            var channel = _connection.CreateModel();

            var sagaExchanges = new[]
            {
                "Shared.Contracts.Events.Saga:IStartPaymentOrchestration",
                "Shared.Contracts.Events.Saga:IOtpVerified",
                "Shared.Contracts.Events.Saga:IOtpFailed",
                "Shared.Contracts.Events.Saga:IPaymentProcessSucceeded",
                "Shared.Contracts.Events.Saga:IPaymentProcessFailed",
                "Shared.Contracts.Events.Saga:IBillUpdateSucceeded",
                "Shared.Contracts.Events.Saga:IBillUpdateFailed",
                "Shared.Contracts.Events.Saga:ICardDeductionSucceeded",
                "Shared.Contracts.Events.Saga:ICardDeductionFailed",
                "Shared.Contracts.Events.Saga:IRevertBillUpdateSucceeded",
                "Shared.Contracts.Events.Saga:IRevertBillUpdateFailed",
                "Shared.Contracts.Events.Saga:IRevertPaymentSucceeded",
                "Shared.Contracts.Events.Saga:IRevertPaymentFailed"
            };

            foreach (var exchange in sagaExchanges)
            {
                try
                {
                    channel.ExchangeDeclare(exchange, ExchangeType.Fanout, durable: true);
                    channel.ExchangeBind("payment-orchestration", exchange, "");
                    _logger.LogInformation("Bound exchange: {Exchange}", exchange);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to bind exchange: {Exchange}", exchange);
                }
            }

            _logger.LogInformation("RabbitMQ bindings configured successfully");
            channel.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure RabbitMQ bindings");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _connection?.Close();
        return Task.CompletedTask;
    }
}

public static class RabbitMqBindingExtensions
{
    public static IServiceCollection AddRabbitMqBindings(this IServiceCollection services)
    {
        services.AddHostedService<RabbitMqBindingInitializer>();
        return services;
    }
}
