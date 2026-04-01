using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using PaymentService.Domain.Interfaces;
using Shared.Contracts.Events.Saga;

namespace PaymentService.Application.Commands.Payments;

public record VerifyOtpCommand(
    Guid PaymentId,
    string OtpCode
) : IRequest<VerifyOtpResult>;

public record VerifyOtpResult(bool Success, string? Error);

public class VerifyOtpCommandHandler(
    IPaymentRepository paymentRepository,
    IPublishEndpoint publishEndpoint,
    ILogger<VerifyOtpCommandHandler> logger
) : IRequestHandler<VerifyOtpCommand, VerifyOtpResult>
{
    public async Task<VerifyOtpResult> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("VerifyOtp: PaymentId={PaymentId}, OtpCode={OtpCode}", 
            request.PaymentId, request.OtpCode);

        var payment = await paymentRepository.GetByIdAsync(request.PaymentId);

        if (payment == null)
        {
            logger.LogWarning("Payment not found: PaymentId={PaymentId}", request.PaymentId);
            return new VerifyOtpResult(false, "Payment not found");
        }

        logger.LogInformation("Publishing IOtpVerified for PaymentId={PaymentId}", request.PaymentId);

        await publishEndpoint.Publish<IOtpVerified>(new
        {
            CorrelationId = request.PaymentId,
            PaymentId = request.PaymentId,
            OtpCode = request.OtpCode,
            VerifiedAt = DateTime.UtcNow
        }, cancellationToken);

        return new VerifyOtpResult(true, null);
    }
}
