using FluentAssertions;
using Xunit;
using BillingService.Application.Commands.Bills;
using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Contracts.Models;
using Shared.Contracts.Events.Saga;

namespace BillingService.Tests.Commands.Bills;

public class CheckOverdueBillsCommandTests
{
    private readonly Mock<IBillRepository> _billRepositoryMock;
    private readonly Mock<IStatementRepository> _statementRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPublishEndpoint> _publisherMock;
    private readonly Mock<ILogger<CheckOverdueBillsCommandHandler>> _loggerMock;
    private readonly CheckOverdueBillsCommandHandler _handler;

    public CheckOverdueBillsCommandTests()
    {
        _billRepositoryMock = new Mock<IBillRepository>();
        _statementRepositoryMock = new Mock<IStatementRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _publisherMock = new Mock<IPublishEndpoint>();
        _loggerMock = new Mock<ILogger<CheckOverdueBillsCommandHandler>>();

        _handler = new CheckOverdueBillsCommandHandler(
            _billRepositoryMock.Object,
            _statementRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _publisherMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_NoPendingBills_ReturnsZeroChecked()
    {
        _billRepositoryMock.Setup(x => x.GetOverdueBillsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Bill>());

        var command = new CheckOverdueBillsCommand();

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.BillsChecked.Should().Be(0);
    }

    [Fact]
    public async Task Handle_BillNotOverdue_SkipsOverdueProcessing()
    {
        var bill = CreateTestBill();
        bill.DueDateUtc = DateTime.UtcNow.AddDays(10);
        _billRepositoryMock.Setup(x => x.GetOverdueBillsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Bill> { bill });

        var command = new CheckOverdueBillsCommand();

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.OverdueCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OverdueBill_MarksOverdueAndPublishesEvent()
    {
        var bill = CreateTestBill();
        bill.DueDateUtc = DateTime.UtcNow.AddDays(-5);
        bill.Status = Domain.Entities.BillStatus.Pending;
        _billRepositoryMock.Setup(x => x.GetOverdueBillsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Bill> { bill });
        _statementRepositoryMock.Setup(x => x.GetByBillIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Statement?)null);

        var command = new CheckOverdueBillsCommand();

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.OverdueCount.Should().Be(1);
        _publisherMock.Verify(x => x.Publish<IBillOverdueDetected>(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Bill CreateTestBill() => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        CardId = Guid.NewGuid(),
        Amount = 500,
        AmountPaid = 0,
        Status = Domain.Entities.BillStatus.Pending,
        DueDateUtc = DateTime.UtcNow.AddDays(-5)
    };
}