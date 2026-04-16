using FluentAssertions;
using Xunit;
using CardService.Application.Queries.Cards;
using CardService.Application.Abstractions.Persistence;
using CardService.Domain.Entities;
using Moq;
using Shared.Contracts.DTOs.Card.Responses;

namespace CardService.Tests.Queries.Cards;

public class ListMyCardsQueryTests
{
    private readonly Mock<ICardRepository> _cardRepositoryMock;
    private readonly ListMyCardsQueryHandler _handler;

    public ListMyCardsQueryTests()
    {
        _cardRepositoryMock = new Mock<ICardRepository>();
        _handler = new ListMyCardsQueryHandler(_cardRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_NoCards_ReturnsEmptyList()
    {
        _cardRepositoryMock.Setup(x => x.ListByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CreditCard>());

        var query = new ListMyCardsQuery(Guid.NewGuid());

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithCards_ReturnsCards()
    {
        var cards = new List<CreditCard> { CreateTestCard(), CreateTestCard() };
        _cardRepositoryMock.Setup(x => x.ListByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cards);

        var query = new ListMyCardsQuery(Guid.NewGuid());

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Cards.Should().HaveCount(2);
    }

    private static CreditCard CreateTestCard() => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        CardholderName = "Test User",
        Last4 = "1111",
        ExpMonth = 12,
        ExpYear = 2030,
        IsDefault = true
    };
}