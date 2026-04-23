using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MassTransit;
using PaymentService.Domain.Enums;
using PaymentService.Infrastructure.Persistence.Sql;
using Shared.Contracts.Events.Saga;

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

            await Task.Delay(_interval, stoppingToken);     // wait 1 minute before next run
        }
    }

    private async Task ExpireOldPaymentsAsync(CancellationToken ct)
    {
        // scope -> a short-lived container  :
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var sendEndpointProvider = scope.ServiceProvider.GetRequiredService<ISendEndpointProvider>();

        var now = DateTime.UtcNow;

        // Find saga instances that are still waiting OTP beyond expiry.
        var expiredAwaitingSagaIds = await context.PaymentOrchestrationSagas
            .Where(s => s.CurrentState == "AwaitingOtpVerification"
                     && s.OtpExpiresAtUtc.HasValue
                     && s.OtpExpiresAtUtc.Value < now)
            .Select(s => s.CorrelationId)
            .ToListAsync(ct);

        if (expiredAwaitingSagaIds.Count == 0)
            return;

        var paymentsToExpire = await context.Payments
            .Where(p => expiredAwaitingSagaIds.Contains(p.Id)
                     && p.Status == PaymentStatus.Initiated)
            .ToListAsync(ct);

        foreach (var payment in paymentsToExpire)
        {
            payment.Status = PaymentStatus.Expired;
            payment.FailureReason = $"Payment expired - OTP not verified within time limit. Expired at: {payment.OtpExpiresAtUtc}";
            payment.OtpCode = null;
            payment.OtpExpiresAtUtc = null;
            payment.UpdatedAtUtc = now;
        }

        if (paymentsToExpire.Count > 0)
        {
            await context.SaveChangesAsync(ct);
            _logger.LogInformation("Marked {Count} payments as Expired", paymentsToExpire.Count);
        }

        // Drive the saga state out of AwaitingOtpVerification for both newly expired
        // and previously expired records that were left hanging.
        var endpoint = await sendEndpointProvider.GetSendEndpoint(new Uri("queue:payment-orchestration"));

        var notifiedCount = 0;
        foreach (var correlationId in expiredAwaitingSagaIds)
        {
            var payment = await context.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == correlationId, ct);
            if (payment == null)
                continue;

            if (payment.Status is PaymentStatus.Initiated or PaymentStatus.Expired)
            {
                await endpoint.Send<IOtpFailed>(new
                {
                    CorrelationId = payment.Id,
                    PaymentId = payment.Id,
                    Reason = "OTP verification timed out",
                    FailedAt = now
                }, ct);

                notifiedCount++;
            }
        }

        if (notifiedCount > 0)
        {
            _logger.LogInformation("Published IOtpFailed for {Count} expired OTP saga(s)", notifiedCount);
        }
    }
}
