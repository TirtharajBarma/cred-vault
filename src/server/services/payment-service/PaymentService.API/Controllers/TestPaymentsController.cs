using Microsoft.AspNetCore.Mvc;
using MediatR;
using PaymentService.Application.Commands.Payments;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Interfaces;
using Shared.Contracts.Controllers;
using Shared.Contracts.Models;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace PaymentService.API.Controllers;

[ApiController]
[Route("api/test/payments")]
public class TestPaymentsController(
    IMediator mediator,
    IPaymentRepository paymentRepository,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publishEndpoint,
    ILogger<TestPaymentsController> logger) : BaseApiController
{
    private static readonly Guid TestUserId = Guid.Parse("019E1D84-20C7-4C01-99AB-1C53CFA39CE3");
    private static readonly Guid TestCardId = Guid.Parse("28DC52A4-6E97-4FB7-A7A0-DEB68437A2D9");
    private static readonly Guid TestBillId = Guid.Parse("6C55FF43-F01A-49E7-8706-5D203FE2DECC");

    [HttpPost("initiate")]
    public async Task<IActionResult> InitiateTestPayment(CancellationToken cancellationToken)
    {
        logger.LogInformation("Test payment initiated for User {UserId}", TestUserId);

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            CardId = TestCardId,
            BillId = TestBillId,
            Amount = 250,
            PaymentType = PaymentType.Full,
            Status = PaymentStatus.Initiated,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await paymentRepository.AddAsync(payment);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var riskScore = 20m;
        var otpCode = Random.Shared.Next(100000, 999999).ToString();

        logger.LogInformation("Starting SAGA orchestration for PaymentId={PaymentId}, OTP={OtpCode}",
            payment.Id, otpCode);

        await publishEndpoint.Publish<Shared.Contracts.Events.Saga.IStartPaymentOrchestration>(new
        {
            CorrelationId = payment.Id,
            PaymentId = payment.Id,
            UserId = payment.UserId,
            Email = "test@test.com",
            FullName = "Test User",
            CardId = payment.CardId,
            BillId = payment.BillId,
            Amount = payment.Amount,
            PaymentType = payment.PaymentType.ToString(),
            RiskScore = riskScore,
            OtpCode = otpCode,
            StartedAt = DateTime.UtcNow
        }, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new ApiResponse<object>
        {
            Success = true,
            Message = "Test payment initiated.",
            Data = new
            {
                PaymentId = payment.Id,
                OtpRequired = true,
                Status = "Pending OTP Verification",
                OtpCode = otpCode,
                DevTestMode = true,
                Instruction = "Use the OTP above to verify payment"
            },
            TraceId = HttpContext.TraceIdentifier
        });
    }

    [HttpPost("{paymentId:guid}/verify-otp")]
    public async Task<IActionResult> VerifyOtpTest(Guid paymentId, [FromBody] TestVerifyOtpRequest request, CancellationToken cancellationToken)
    {
        var command = new VerifyOtpCommand(paymentId, request.OtpCode);
        var result = await mediator.Send(command, cancellationToken);

        if (!result.Success)
            return BadRequest(Fail(result.Error ?? "OTP verification failed."));

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "OTP verified. Payment is processing.",
            Data = new { PaymentId = paymentId, Status = "Processing" },
            TraceId = HttpContext.TraceIdentifier
        });
    }
}

public record TestVerifyOtpRequest(string OtpCode);
