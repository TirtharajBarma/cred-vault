using MassTransit;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Services;
using Shared.Contracts.Events.Identity;

namespace PaymentService.Infrastructure.Messaging.Consumers;

public sealed class UserRegisteredConsumer : IConsumer<IUserRegistered>
{
    private readonly IWalletService _walletService;
    private readonly ILogger<UserRegisteredConsumer> _logger;

    public UserRegisteredConsumer(IWalletService walletService, ILogger<UserRegisteredConsumer> logger)
    {
        _walletService = walletService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<IUserRegistered> context)
    {
        var userId = context.Message.UserId;
        _logger.LogInformation("Received IUserRegistered for UserId={UserId}. Creating wallet...", userId);

        try
        {
            var wallet = await _walletService.CreateWalletAsync(userId, context.CancellationToken);
            _logger.LogInformation("Created wallet {WalletId} for user {UserId}", wallet.Id, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create wallet for user {UserId}", userId);
            throw;
        }
    }
}
