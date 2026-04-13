using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PaymentService.Domain.Enums;
using PaymentService.Infrastructure.Persistence.Sql;

namespace PaymentService.Infrastructure.BackgroundJobs;

public class PaymentExpirationBackgroundJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PaymentExpirationBackgroundJob> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);      // runs every 1minute

    public PaymentExpirationBackgroundJob(
        IServiceProvider serviceProvider,
        ILogger<PaymentExpirationBackgroundJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Payment Expiration Background Job started");

        while (!stoppingToken.IsCancellationRequested)              // infinite loop until app shuts down
        {
            try
            {
                await ExpireOldPaymentsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Payment Expiration Background Job");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task ExpireOldPaymentsAsync(CancellationToken ct)
    {
        // scope -> a short-lived container  : 
        using var scope = _serviceProvider.CreateScope();                           // create a new DI scope
        var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();     // gets your DbContext -> sage usage, proper lifecycle

        var now = DateTime.UtcNow;
        
        var expiredPayments = await context.Payments            // find expired payment
            .Where(p => p.Status == PaymentStatus.Initiated 
                     && p.OtpExpiresAtUtc.HasValue 
                     && p.OtpExpiresAtUtc.Value < now)
            .ToListAsync(ct);

        if (expiredPayments.Count == 0)
            return;

        _logger.LogInformation("Found {Count} expired payments to mark as Expired", expiredPayments.Count);

        foreach (var payment in expiredPayments)
        {
            payment.Status = PaymentStatus.Expired;
            payment.FailureReason = $"Payment expired - OTP not verified within time limit. Expired at: {payment.OtpExpiresAtUtc}";
            payment.UpdatedAtUtc = now;
        }

        await context.SaveChangesAsync(ct);
        
        _logger.LogInformation("Marked {Count} payments as Expired", expiredPayments.Count);
    }
}
