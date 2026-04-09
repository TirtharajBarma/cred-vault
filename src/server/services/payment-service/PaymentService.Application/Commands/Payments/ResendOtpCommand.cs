using System.Security.Cryptography;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Interfaces;
using Shared.Contracts.Events.Payment;

namespace PaymentService.Application.Commands.Payments;

public record ResendOtpCommand(Guid PaymentId, Guid UserId, string AuthorizationHeader) : IRequest<ResendOtpResult>;

public record ResendOtpResult(bool Success, string? Error, DateTime? ExpiresAtUtc = null);

public class ResendOtpCommandHandler(
    IPaymentRepository paymentRepository,
    IPublishEndpoint publishEndpoint,
    ILogger<ResendOtpCommandHandler> logger
) : IRequestHandler<ResendOtpCommand, ResendOtpResult>
{
    public async Task<ResendOtpResult> Handle(ResendOtpCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("ResendOtp: PaymentId={PaymentId}", request.PaymentId);

        var payment = await paymentRepository.GetByIdAsync(request.PaymentId);

        if (payment == null)
        {
            logger.LogWarning("Payment not found: PaymentId={PaymentId}", request.PaymentId);
            return new ResendOtpResult(false, "Payment not found");
        }

        if (payment.UserId != request.UserId)
        {
            logger.LogWarning("Authorization failed: Payment {PaymentId} belongs to {Owner}, not {Requester}",
                request.PaymentId, payment.UserId, request.UserId);
            return new ResendOtpResult(false, "Not authorized");
        }

        if (payment.Status != PaymentStatus.Initiated)
        {
            logger.LogWarning("Payment {PaymentId} is not in Initiated status", request.PaymentId);
            return new ResendOtpResult(false, "Payment is not pending verification");
        }

        var newOtpCode = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(10);

        payment.OtpCode = newOtpCode;
        payment.OtpExpiresAtUtc = expiresAtUtc;
        payment.UpdatedAtUtc = DateTime.UtcNow;

        await paymentRepository.UpdateAsync(payment);

        // Publish OTP event so notification service delivers it to the user
        await publishEndpoint.Publish<IPaymentOtpGenerated>(new
        {
            PaymentId = payment.Id,
            UserId = payment.UserId,
            Email = string.Empty,
            FullName = string.Empty,
            Amount = payment.Amount,
            OtpCode = newOtpCode,
            ExpiresAtUtc = expiresAtUtc
        }, cancellationToken);

        logger.LogInformation("New OTP generated and published for PaymentId={PaymentId}, ExpiresAt={ExpiresAt}",
            request.PaymentId, expiresAtUtc);

        return new ResendOtpResult(true, null, expiresAtUtc);
    }
}
