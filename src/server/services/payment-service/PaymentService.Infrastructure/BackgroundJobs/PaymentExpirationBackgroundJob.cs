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
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

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

        while (!stoppingToken.IsCancellationRequested)
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
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

        var now = DateTime.UtcNow;
        
        var expiredPayments = await context.Payments
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
