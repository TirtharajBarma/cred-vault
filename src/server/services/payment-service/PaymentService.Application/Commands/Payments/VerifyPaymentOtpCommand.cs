using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Events.Payment;

namespace PaymentService.Application.Commands.Payments;

public record VerifyPaymentOtpCommand(Guid PaymentId, string OtpCode) : IRequest<VerifyOtpResult>;

public record VerifyOtpResult(bool Success, string? Error);

public class VerifyPaymentOtpCommandHandler(IPublishEndpoint publishEndpoint, ILogger<VerifyPaymentOtpCommandHandler> logger)
    : IRequestHandler<VerifyPaymentOtpCommand, VerifyOtpResult>
{
    public async Task<VerifyOtpResult> Handle(VerifyPaymentOtpCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Publishing IOTPVerified: PaymentId={PaymentId}", request.PaymentId);

        try
        {
            await publishEndpoint.Publish<IOTPVerified>(new
            {
                PaymentId  = request.PaymentId,
                OtpCode    = request.OtpCode,
                VerifiedAt = DateTime.UtcNow
            }, cancellationToken);

            logger.LogInformation("IOTPVerified published for PaymentId={PaymentId}", request.PaymentId);
            return new VerifyOtpResult(true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish IOTPVerified for PaymentId={PaymentId}", request.PaymentId);
            return new VerifyOtpResult(false, "Failed to verify OTP");
        }
    }
}
