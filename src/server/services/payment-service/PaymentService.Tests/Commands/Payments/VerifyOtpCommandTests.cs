using FluentAssertions;
using Xunit;
using PaymentService.Application.Commands.Payments;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Interfaces;
using PaymentService.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using MassTransit;

namespace PaymentService.Tests.Commands.Payments;

public class VerifyOtpCommandTests
{
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ISendEndpointProvider> _sendEndpointProviderMock;
    private readonly Mock<ILogger<VerifyOtpCommandHandler>> _loggerMock;
    private readonly VerifyOtpCommandHandler _handler;

    public VerifyOtpCommandTests()
    {
        _paymentRepositoryMock = new Mock<IPaymentRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _sendEndpointProviderMock = new Mock<ISendEndpointProvider>();
        _loggerMock = new Mock<ILogger<VerifyOtpCommandHandler>>();
        
        var mockEndpoint = new Mock<ISendEndpoint>();
        _sendEndpointProviderMock.Setup(x => x.GetSendEndpoint(It.IsAny<Uri>()))
            .ReturnsAsync(mockEndpoint.Object);
        
        _handler = new VerifyOtpCommandHandler(
            _paymentRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _sendEndpointProviderMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_PaymentNotFound_ReturnsError()
    {
        _paymentRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Payment?)null);

        var command = new VerifyOtpCommand(Guid.NewGuid(), Guid.NewGuid(), "123456");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Payment not found");
    }

    [Fact]
    public async Task Handle_UserMismatch_ReturnsError()
    {
        var payment = CreateTestPayment();
        payment.UserId = Guid.NewGuid();
        _paymentRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(payment);

        var command = new VerifyOtpCommand(payment.Id, Guid.NewGuid(), "123456");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Not authorized");
    }

    [Fact]
    public async Task Handle_PaymentNotInInitiatedStatus_ReturnsError()
    {
        var payment = CreateTestPayment();
        payment.Status = PaymentStatus.Completed;
        _paymentRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(payment);

        var command = new VerifyOtpCommand(payment.Id, payment.UserId, "123456");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Payment is not pending verification");
    }

    [Fact]
    public async Task Handle_NoOtp_ReturnsError()
    {
        var payment = CreateTestPayment();
        payment.OtpCode = null;
        _paymentRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(payment);

        var command = new VerifyOtpCommand(payment.Id, payment.UserId, "123456");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("OTP not generated");
    }

    [Fact]
    public async Task Handle_ExpiredOtp_ReturnsError()
    {
        var payment = CreateTestPayment();
        payment.OtpExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5);
        _paymentRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(payment);

        var command = new VerifyOtpCommand(payment.Id, payment.UserId, "123456");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("OTP has expired");
    }

    [Fact]
    public async Task Handle_InvalidOtp_ReturnsError()
    {
        var payment = CreateTestPayment();
        payment.OtpCode = "123456";
        payment.OtpExpiresAtUtc = DateTime.UtcNow.AddMinutes(10);
        _paymentRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(payment);

        var command = new VerifyOtpCommand(payment.Id, payment.UserId, "000000");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Invalid OTP code");
    }

    private static Payment CreateTestPayment() => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        CardId = Guid.NewGuid(),
        BillId = Guid.NewGuid(),
        Amount = 100,
        Status = PaymentStatus.Initiated,
        OtpCode = "123456",
        OtpExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };
}