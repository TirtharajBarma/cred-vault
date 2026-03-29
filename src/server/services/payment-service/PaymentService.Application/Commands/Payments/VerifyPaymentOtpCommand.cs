using MassTransit;
using MediatR;
using Shared.Contracts.Events.Payment;

namespace PaymentService.Application.Commands.Payments;

public record VerifyPaymentOtpCommand(Guid PaymentId, string OtpCode) : IRequest<VerifyOtpResult>;

public record VerifyOtpResult(bool Success, string? Error);

// NOTE: The actual OTP validation happens in the controller (API layer) before this command is sent,
// because the controller has access to PaymentDbContext to look up the saga state.
// This command only publishes the OTPVerified event to the saga.
public class VerifyPaymentOtpCommandHandler(IPublishEndpoint publishEndpoint)
    : IRequestHandler<VerifyPaymentOtpCommand, VerifyOtpResult>
{
    public async Task<VerifyOtpResult> Handle(VerifyPaymentOtpCommand request, CancellationToken cancellationToken)
    {
        await publishEndpoint.Publish<IOTPVerified>(new
        {
            PaymentId  = request.PaymentId,
            OtpCode    = request.OtpCode,
            VerifiedAt = DateTime.UtcNow
        }, cancellationToken);

        return new VerifyOtpResult(true, null);
    }
}
