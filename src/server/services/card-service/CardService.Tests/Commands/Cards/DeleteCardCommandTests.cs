using FluentAssertions;
using Xunit;
using CardService.Application.Commands.Cards;
using CardService.Application.Abstractions.Persistence;
using CardService.Application.Interfaces;
using CardService.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Contracts.Exceptions;

namespace CardService.Tests.Commands.Cards;

public class DeleteCardCommandTests
{
    private readonly Mock<ICardRepository> _cardRepositoryMock;
    private readonly Mock<IBillingServiceClient> _billingServiceClientMock;
    private readonly Mock<ILogger<DeleteCardCommandHandler>> _loggerMock;
    private readonly DeleteCardCommandHandler _handler;

    public DeleteCardCommandTests()
    {
        _cardRepositoryMock = new Mock<ICardRepository>();
        _billingServiceClientMock = new Mock<IBillingServiceClient>();
        _loggerMock = new Mock<ILogger<DeleteCardCommandHandler>>();
        _handler = new DeleteCardCommandHandler(_cardRepositoryMock.Object, _billingServiceClientMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_CardNotFound_ThrowsNotFoundException()
    {
        _cardRepositoryMock.Setup(x => x.GetByUserAndIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreditCard?)null);

        var command = new DeleteCardCommand(Guid.NewGuid(), Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_OutstandingBalance_ThrowsForbiddenException()
    {
        var card = CreateTestCard();
        card.OutstandingBalance = 100;
        _cardRepositoryMock.Setup(x => x.GetByUserAndIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(card);

        var command = new DeleteCardCommand(Guid.NewGuid(), Guid.NewGuid());

        await Assert.ThrowsAsync<ForbiddenException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_PendingBill_ThrowsForbiddenException()
    {
        var card = CreateTestCard();
        card.OutstandingBalance = 0;
        _cardRepositoryMock.Setup(x => x.GetByUserAndIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(card);
        _billingServiceClientMock.Setup(x => x.HasPendingBillAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new DeleteCardCommand(Guid.NewGuid(), Guid.NewGuid());

        await Assert.ThrowsAsync<ForbiddenException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Success_DeletesCard()
    {
        var card = CreateTestCard();
        card.OutstandingBalance = 0;
        _cardRepositoryMock.Setup(x => x.GetByUserAndIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(card);
        _billingServiceClientMock.Setup(x => x.HasPendingBillAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new DeleteCardCommand(Guid.NewGuid(), card.Id);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        _cardRepositoryMock.Verify(x => x.DeleteAsync(It.IsAny<CreditCard>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static CreditCard CreateTestCard() => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        CardholderName = "Test User",
        Last4 = "1111",
        OutstandingBalance = 0,
        ExpMonth = 12,
        ExpYear = 2030
    };
}