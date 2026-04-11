using FluentAssertions;
using Xunit;
using PaymentService.Application.Queries.Payments;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Interfaces;
using PaymentService.Domain.Entities;
using Moq;

namespace PaymentService.Tests.Queries.Payments;

public class GetPaymentByIdQueryTests
{
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock;
    private readonly GetPaymentByIdQueryHandler _handler;

    public GetPaymentByIdQueryTests()
    {
        _paymentRepositoryMock = new Mock<IPaymentRepository>();
        _handler = new GetPaymentByIdQueryHandler(_paymentRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_PaymentNotFound_ReturnsNull()
    {
        _paymentRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Payment?)null);

        var query = new GetPaymentByIdQuery(Guid.NewGuid(), Guid.NewGuid());

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_UserMismatch_ReturnsNull()
    {
        var payment = CreateTestPayment();
        _paymentRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(payment);

        var query = new GetPaymentByIdQuery(payment.Id, Guid.NewGuid());

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Success_ReturnsPayment()
    {
        var payment = CreateTestPayment();
        _paymentRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(payment);

        var query = new GetPaymentByIdQuery(payment.Id, payment.UserId);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(payment.Id);
    }

    private static Payment CreateTestPayment() => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        CardId = Guid.NewGuid(),
        BillId = Guid.NewGuid(),
        Amount = 100,
        Status = PaymentStatus.Completed,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };
}