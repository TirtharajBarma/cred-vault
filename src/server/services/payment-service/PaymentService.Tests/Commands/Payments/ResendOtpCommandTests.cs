using FluentAssertions;
using Xunit;
using PaymentService.Application.Commands.Payments;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Interfaces;
using PaymentService.Domain.Entities;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;

namespace PaymentService.Tests.Commands.Payments;

public class ResendOtpCommandTests
{
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock;
    private readonly Mock<IPublishEndpoint> _publisherMock;
    private readonly Mock<ILogger<ResendOtpCommandHandler>> _loggerMock;
    private readonly ResendOtpCommandHandler _handler;

    public ResendOtpCommandTests()
    {
        _paymentRepositoryMock = new Mock<IPaymentRepository>();
        _publisherMock = new Mock<IPublishEndpoint>();
        _loggerMock = new Mock<ILogger<ResendOtpCommandHandler>>();
        _handler = new ResendOtpCommandHandler(
            _paymentRepositoryMock.Object,
            _publisherMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_PaymentNotFound_ReturnsError()
    {
        _paymentRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Payment?)null);

        var command = new ResendOtpCommand(Guid.NewGuid(), Guid.NewGuid(), "Bearer token");

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

        var command = new ResendOtpCommand(payment.Id, Guid.NewGuid(), "Bearer token");

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

        var command = new ResendOtpCommand(payment.Id, payment.UserId, "Bearer token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Payment is not pending verification");
    }

    [Fact]
    public async Task Handle_Success_GeneratesNewOtp()
    {
        var payment = CreateTestPayment();
        _paymentRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(payment);
        _paymentRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Payment>()))
            .Returns(Task.CompletedTask);

        var command = new ResendOtpCommand(payment.Id, payment.UserId, "Bearer token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ExpiresAtUtc.Should().NotBeNull();
        payment.OtpCode.Should().NotBeNull();
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