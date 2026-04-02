using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Interfaces;
using Shared.Contracts.Events.Saga;

namespace PaymentService.Application.Commands.Payments;

public record VerifyOtpCommand(Guid PaymentId, Guid UserId, string OtpCode) : IRequest<VerifyOtpResult>;

public record VerifyOtpResult(bool Success, string? Error);

public class VerifyOtpCommandHandler(
    IPaymentRepository paymentRepository,
    ISendEndpointProvider sendEndpointProvider,
    ILogger<VerifyOtpCommandHandler> logger
) : IRequestHandler<VerifyOtpCommand, VerifyOtpResult>
{
    public async Task<VerifyOtpResult> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("VerifyOtp: PaymentId={PaymentId}", request.PaymentId);

        var payment = await paymentRepository.GetByIdAsync(request.PaymentId);

        if (payment == null)
        {
            logger.LogWarning("Payment not found: PaymentId={PaymentId}", request.PaymentId);
            return new VerifyOtpResult(false, "Payment not found");
        }

        if (payment.UserId != request.UserId)
        {
            logger.LogWarning("Authorization failed: Payment {PaymentId} belongs to {Owner}, not {Requester}", 
                request.PaymentId, payment.UserId, request.UserId);
            return new VerifyOtpResult(false, "Not authorized");
        }

        if (payment.Status != PaymentStatus.Initiated)
        {
            logger.LogWarning("Payment {PaymentId} is not in Initiated status", request.PaymentId);
            return new VerifyOtpResult(false, "Payment is not pending verification");
        }

        if (payment.OtpCode == null || payment.OtpExpiresAtUtc == null)
        {
            logger.LogWarning("Payment {PaymentId} has no OTP", request.PaymentId);
            return new VerifyOtpResult(false, "OTP not generated");
        }

        if (payment.OtpCode != request.OtpCode)
        {
            logger.LogWarning("Invalid OTP for Payment {PaymentId}", request.PaymentId);
            var endpoint = await sendEndpointProvider.GetSendEndpoint(new Uri("queue:payment-orchestration"));
            await endpoint.Send<IOtpFailed>(new
            {
                CorrelationId = request.PaymentId,
                PaymentId = request.PaymentId,
                Reason = "Invalid OTP code",
                FailedAt = DateTime.UtcNow
            }, cancellationToken);
            return new VerifyOtpResult(false, "Invalid OTP code");
        }

        if (payment.OtpExpiresAtUtc < DateTime.UtcNow)
        {
            logger.LogWarning("Expired OTP for Payment {PaymentId}", request.PaymentId);
            var endpoint = await sendEndpointProvider.GetSendEndpoint(new Uri("queue:payment-orchestration"));
            await endpoint.Send<IOtpFailed>(new
            {
                CorrelationId = request.PaymentId,
                PaymentId = request.PaymentId,
                Reason = "OTP has expired",
                FailedAt = DateTime.UtcNow
            }, cancellationToken);
            return new VerifyOtpResult(false, "OTP has expired");
        }

        logger.LogInformation("Publishing IOtpVerified for PaymentId={PaymentId}", request.PaymentId);

        var successEndpoint = await sendEndpointProvider.GetSendEndpoint(new Uri("queue:payment-orchestration"));
        await successEndpoint.Send<IOtpVerified>(new
        {
            CorrelationId = request.PaymentId,
            PaymentId = request.PaymentId,
            OtpCode = request.OtpCode,
            VerifiedAt = DateTime.UtcNow
        }, cancellationToken);

        return new VerifyOtpResult(true, null);
    }
}
