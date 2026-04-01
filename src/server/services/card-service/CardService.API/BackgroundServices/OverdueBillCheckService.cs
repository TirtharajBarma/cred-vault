using CardService.Application.Abstractions.Persistence;
using CardService.Application.Commands.Cards;
using CardService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CardService.API.BackgroundServices;

public class OverdueBillCheckService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OverdueBillCheckService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24);

    public OverdueBillCheckService(IServiceProvider serviceProvider, ILogger<OverdueBillCheckService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OverdueBillCheckService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckOverdueBillsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OverdueBillCheckService");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CheckOverdueBillsAsync(CancellationToken ct)
    {
        _logger.LogInformation("Checking for overdue bills...");

        using var scope = _serviceProvider.CreateScope();
        var cards = scope.ServiceProvider.GetRequiredService<ICardRepository>();
        var violations = scope.ServiceProvider.GetRequiredService<IViolationRepository>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var activeCards = await cards.GetAllActiveCardsWithBalanceAsync(ct);

        foreach (var card in activeCards)
        {
            try
            {
                await ProcessCardForOverdueAsync(card, mediator, violations, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing card {CardId}", card.Id);
            }
        }

        _logger.LogInformation("Overdue check completed for {Count} cards", activeCards.Count);
    }

    private async Task ProcessCardForOverdueAsync(CreditCard card, IMediator mediator, IViolationRepository violations, CancellationToken ct)
    {
        var existingViolation = await violations.GetActiveViolationByCardIdAsync(card.Id, ct);
        
        if (existingViolation != null)
        {
            _logger.LogDebug("Card {CardId} already has active violation", card.Id);
            return;
        }

        if (card.StrikeCount >= 3)
        {
            _logger.LogDebug("Card {CardId} already has max strikes", card.Id);
            return;
        }

        var reason = $"Overdue bill detected - Balance: {card.OutstandingBalance}, BillingCycle: {card.BillingCycleStartDay}";
        
        var result = await mediator.Send(
            new ApplyStrikeCommand(card.Id, Guid.Empty, reason), 
            ct);

        if (result.Success)
        {
            _logger.LogInformation(
                "Strike applied to card {CardId}: Count={Count}, Blocked={Blocked}",
                card.Id, result.Data?.NewStrikeCount, result.Data?.IsCardBlocked);
        }
    }
}
