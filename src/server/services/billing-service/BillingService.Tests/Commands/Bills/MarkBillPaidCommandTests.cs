using FluentAssertions;
using Xunit;
using BillingService.Application.Commands.Bills;
using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Contracts.Models;
using Shared.Contracts.Exceptions;
using Shared.Contracts.Enums;

namespace BillingService.Tests.Commands.Bills;

public class MarkBillPaidCommandTests
{
    private readonly Mock<IBillRepository> _billRepositoryMock;
    private readonly Mock<IRewardRepository> _rewardRepositoryMock;
    private readonly Mock<IStatementRepository> _statementRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<MarkBillPaidCommandHandler>> _loggerMock;
    private readonly MarkBillPaidCommandHandler _handler;

    public MarkBillPaidCommandTests()
    {
        _billRepositoryMock = new Mock<IBillRepository>();
        _rewardRepositoryMock = new Mock<IRewardRepository>();
        _statementRepositoryMock = new Mock<IStatementRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<MarkBillPaidCommandHandler>>();

        _handler = new MarkBillPaidCommandHandler(
            _billRepositoryMock.Object,
            _rewardRepositoryMock.Object,
            _statementRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_BillNotFound_ThrowsNotFoundException()
    {
        _billRepositoryMock.Setup(x => x.GetByIdAndUserIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Bill?)null);

        var command = new MarkBillPaidCommand(Guid.NewGuid(), Guid.NewGuid(), 100);

        await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AlreadyPaid_ReturnsSuccessWithNoChange()
    {
        var bill = CreateTestBill();
        bill.Status = Domain.Entities.BillStatus.Paid;
        bill.AmountPaid = bill.Amount;
        _billRepositoryMock.Setup(x => x.GetByIdAndUserIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bill);
        _rewardRepositoryMock.Setup(x => x.HasTransactionForBillAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new MarkBillPaidCommand(bill.UserId, bill.Id, 100);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("already paid");
    }

    [Fact]
    public async Task Handle_NegativeAmount_ThrowsValidationException()
    {
        var bill = CreateTestBill();
        _billRepositoryMock.Setup(x => x.GetByIdAndUserIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bill);

        var command = new MarkBillPaidCommand(bill.UserId, bill.Id, -50);

        await Assert.ThrowsAsync<ValidationException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ZeroAmount_ThrowsValidationException()
    {
        var bill = CreateTestBill();
        _billRepositoryMock.Setup(x => x.GetByIdAndUserIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bill);

        var command = new MarkBillPaidCommand(bill.UserId, bill.Id, 0);

        await Assert.ThrowsAsync<ValidationException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_FirstPaymentLessThanMinDue_ReturnsValidationError()
    {
        var bill = CreateTestBill();
        bill.AmountPaid = 0;
        bill.MinDue = 50;
        _billRepositoryMock.Setup(x => x.GetByIdAndUserIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bill);

        var command = new MarkBillPaidCommand(bill.UserId, bill.Id, 30);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("minimum due");
    }

    [Fact]
    public async Task Handle_PartialPayment_LeavesBillAsPartiallyPaid()
    {
        // Arrange: Bill with 500 amount, 0 paid
        var bill = CreateTestBill();
        bill.Amount = 500;
        bill.MinDue = 50;
        bill.AmountPaid = 0;
        bill.Status = Domain.Entities.BillStatus.Pending;
        
        _billRepositoryMock.Setup(x => x.GetByIdAndUserIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bill);
        _rewardRepositoryMock.Setup(x => x.HasTransactionForBillAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _rewardRepositoryMock.Setup(x => x.GetBestMatchingTierAsync(It.IsAny<CardNetwork>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RewardTier?)null);
        _statementRepositoryMock.Setup(x => x.GetByBillIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Statement?)null);

        // Act: Pay only 50 (minimum due) on a 500 bill
        var command = new MarkBillPaidCommand(bill.UserId, bill.Id, 50);
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        bill.AmountPaid.Should().Be(50);
        bill.Status.Should().Be(Domain.Entities.BillStatus.PartiallyPaid);
        result.Message.Should().Contain("Remaining");
    }

    [Fact]
    public async Task Handle_SecondPartialPayment_UpdatesBillStatus()
    {
        // Arrange: Bill with 500 amount, already paid 50
        var bill = CreateTestBill();
        bill.Amount = 500;
        bill.MinDue = 50;
        bill.AmountPaid = 50;
        bill.Status = Domain.Entities.BillStatus.PartiallyPaid;
        
        _billRepositoryMock.Setup(x => x.GetByIdAndUserIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bill);
        _rewardRepositoryMock.Setup(x => x.HasTransactionForBillAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _rewardRepositoryMock.Setup(x => x.GetBestMatchingTierAsync(It.IsAny<CardNetwork>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RewardTier?)null);
        _statementRepositoryMock.Setup(x => x.GetByBillIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Statement?)null);

        // Act: Pay remaining 450 to fully settle
        var command = new MarkBillPaidCommand(bill.UserId, bill.Id, 450);
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        bill.AmountPaid.Should().Be(500);
        bill.Status.Should().Be(Domain.Entities.BillStatus.Paid);
        bill.PaidAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_FullPayment_SetsStatusToPaid()
    {
        var bill = CreateTestBill();
        bill.Amount = 500;
        bill.AmountPaid = 0;
        _billRepositoryMock.Setup(x => x.GetByIdAndUserIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bill);
        _rewardRepositoryMock.Setup(x => x.HasTransactionForBillAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _rewardRepositoryMock.Setup(x => x.GetBestMatchingTierAsync(It.IsAny<CardNetwork>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RewardTier?)null);
        _statementRepositoryMock.Setup(x => x.GetByBillIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Statement?)null);

        var command = new MarkBillPaidCommand(bill.UserId, bill.Id, 500);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        bill.Status.Should().Be(Domain.Entities.BillStatus.Paid);
        bill.PaidAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_PaymentExceedsBillAmount_CapsAtBillAmount()
    {
        var bill = CreateTestBill();
        bill.Amount = 500;
        bill.AmountPaid = 0;
        _billRepositoryMock.Setup(x => x.GetByIdAndUserIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bill);
        _rewardRepositoryMock.Setup(x => x.HasTransactionForBillAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _rewardRepositoryMock.Setup(x => x.GetBestMatchingTierAsync(It.IsAny<CardNetwork>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RewardTier?)null);
        _statementRepositoryMock.Setup(x => x.GetByBillIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Statement?)null);

        var command = new MarkBillPaidCommand(bill.UserId, bill.Id, 600);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        bill.AmountPaid.Should().Be(500); // Capped at bill amount
        bill.Status.Should().Be(Domain.Entities.BillStatus.Paid);
    }

    [Fact]
    public async Task Handle_PartialPaymentExceedsBillAmount_CapsAndMarksPaid()
    {
        var bill = CreateTestBill();
        bill.Amount = 500;
        bill.AmountPaid = 400;
        bill.Status = Domain.Entities.BillStatus.PartiallyPaid;
        _billRepositoryMock.Setup(x => x.GetByIdAndUserIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bill);
        _rewardRepositoryMock.Setup(x => x.HasTransactionForBillAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _rewardRepositoryMock.Setup(x => x.GetBestMatchingTierAsync(It.IsAny<CardNetwork>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RewardTier?)null);
        _statementRepositoryMock.Setup(x => x.GetByBillIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Statement?)null);

        // Pay 200 when only 100 remaining
        var command = new MarkBillPaidCommand(bill.UserId, bill.Id, 200);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        bill.AmountPaid.Should().Be(500); // Capped
        bill.Status.Should().Be(Domain.Entities.BillStatus.Paid);
    }

    private static Bill CreateTestBill() => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        CardId = Guid.NewGuid(),
        CardNetwork = CardNetwork.Visa,
        IssuerId = Guid.NewGuid(),
        Amount = 500,
        MinDue = 50,
        AmountPaid = 0,
        Status = Domain.Entities.BillStatus.Pending,
        BillingDateUtc = DateTime.UtcNow.AddDays(-30),
        DueDateUtc = DateTime.UtcNow.AddDays(30)
    };
}
