using FluentAssertions;
using Xunit;
using BillingService.Application.Commands.Rewards;
using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Contracts.Models;

namespace BillingService.Tests.Commands.Rewards;

public class RedeemRewardsCommandTests
{
    private readonly Mock<IRewardRepository> _rewardRepositoryMock;
    private readonly Mock<IBillRepository> _billRepositoryMock;
    private readonly Mock<IStatementRepository> _statementRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<RedeemRewardsCommandHandler>> _loggerMock;
    private readonly RedeemRewardsCommandHandler _handler;

    public RedeemRewardsCommandTests()
    {
        _rewardRepositoryMock = new Mock<IRewardRepository>();
        _billRepositoryMock = new Mock<IBillRepository>();
        _statementRepositoryMock = new Mock<IStatementRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<RedeemRewardsCommandHandler>>();

        _handler = new RedeemRewardsCommandHandler(
            _rewardRepositoryMock.Object,
            _billRepositoryMock.Object,
            _statementRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_NegativePoints_ReturnsValidationError()
    {
        var command = new RedeemRewardsCommand(Guid.NewGuid(), -10, RedeemRewardsTarget.Account, null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("greater than zero");
    }

    [Fact]
    public async Task Handle_ZeroPoints_ReturnsValidationError()
    {
        var command = new RedeemRewardsCommand(Guid.NewGuid(), 0, RedeemRewardsTarget.Account, null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("greater than zero");
    }

    [Fact]
    public async Task Handle_NoRewardAccount_ReturnsError()
    {
        _rewardRepositoryMock.Setup(x => x.GetAccountByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RewardAccount?)null);

        var command = new RedeemRewardsCommand(Guid.NewGuid(), 100, RedeemRewardsTarget.Account, null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No reward account found");
    }

    [Fact]
    public async Task Handle_BillRedemptionWithoutBillId_ReturnsError()
    {
        var account = CreateTestAccount();
        _rewardRepositoryMock.Setup(x => x.GetAccountByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var command = new RedeemRewardsCommand(Guid.NewGuid(), 100, RedeemRewardsTarget.Bill, null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Bill ID is required");
    }

    [Fact]
    public async Task Handle_BillNotFound_ReturnsError()
    {
        var account = CreateTestAccount();
        account.PointsBalance = 100;
        _rewardRepositoryMock.Setup(x => x.GetAccountByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _billRepositoryMock.Setup(x => x.GetByIdAndUserIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Bill?)null);

        var command = new RedeemRewardsCommand(Guid.NewGuid(), 100, RedeemRewardsTarget.Bill, Guid.NewGuid());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Bill not found");
    }

    [Fact]
    public async Task Handle_AccountRedemption_Success()
    {
        var account = CreateTestAccount();
        account.PointsBalance = 100;
        _rewardRepositoryMock.Setup(x => x.GetAccountByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _rewardRepositoryMock.Setup(x => x.UpdateAccountAsync(It.IsAny<RewardAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _rewardRepositoryMock.Setup(x => x.AddTransactionAsync(It.IsAny<RewardTransaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new RedeemRewardsCommand(Guid.NewGuid(), 50, RedeemRewardsTarget.Account, null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.NewPointsBalance.Should().Be(50);
    }

    private static RewardAccount CreateTestAccount() => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        PointsBalance = 100,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };
}