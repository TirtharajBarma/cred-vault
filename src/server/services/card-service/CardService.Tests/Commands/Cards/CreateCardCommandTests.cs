using FluentAssertions;
using Xunit;
using CardService.Application.Commands.Cards;
using CardService.Application.Abstractions.Persistence;
using CardService.Domain.Entities;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Contracts.DTOs.Card.Responses;
using Shared.Contracts.Enums;
using Shared.Contracts.Events.Card;

namespace CardService.Tests.Commands.Cards;

public class CreateCardCommandTests
{
    private readonly Mock<ICardRepository> _cardRepositoryMock;
    private readonly Mock<IPublishEndpoint> _publisherMock;
    private readonly Mock<ILogger<CreateCardCommandHandler>> _loggerMock;
    private readonly CreateCardCommandHandler _handler;

    public CreateCardCommandTests()
    {
        _cardRepositoryMock = new Mock<ICardRepository>();
        _publisherMock = new Mock<IPublishEndpoint>();
        _loggerMock = new Mock<ILogger<CreateCardCommandHandler>>();
        _handler = new CreateCardCommandHandler(_cardRepositoryMock.Object, _publisherMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_EmptyUserId_ReturnsForbiddenError()
    {
        var command = new CreateCardCommand(Guid.Empty, "Test User", 12, 2025, "4111111111111111", Guid.NewGuid(), false, "encrypted");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("Forbidden");
    }

    [Fact]
    public async Task Handle_EmptyCardNumber_ReturnsValidationError()
    {
        var command = new CreateCardCommand(Guid.NewGuid(), "Test User", 12, 2025, "", Guid.NewGuid(), false, "encrypted");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("ValidationError");
    }

    [Fact]
    public async Task Handle_EmptyCardholderName_ReturnsValidationError()
    {
        var command = new CreateCardCommand(Guid.NewGuid(), "", 12, 2025, "4111111111111111", Guid.NewGuid(), false, "encrypted");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("ValidationError");
    }

    [Fact]
    public async Task Handle_InvalidExpMonth_ReturnsValidationError()
    {
        var command = new CreateCardCommand(Guid.NewGuid(), "Test User", 0, 2025, "4111111111111111", Guid.NewGuid(), false, "encrypted");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Expiration month");
    }

    [Fact]
    public async Task Handle_InvalidExpMonthAbove12_ReturnsValidationError()
    {
        var command = new CreateCardCommand(Guid.NewGuid(), "Test User", 13, 2025, "4111111111111111", Guid.NewGuid(), false, "encrypted");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Expiration month");
    }

    [Fact]
    public async Task Handle_InvalidExpYear_ReturnsValidationError()
    {
        var command = new CreateCardCommand(Guid.NewGuid(), "Test User", 12, 2020, "4111111111111111", Guid.NewGuid(), false, "encrypted");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Expiration year");
    }

    [Fact]
    public async Task Handle_IssuerNotFound_ReturnsError()
    {
        _cardRepositoryMock.Setup(x => x.GetIssuerByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CardIssuer?)null);

        var command = new CreateCardCommand(Guid.NewGuid(), "Test User", 12, 2030, "4111111111111111", Guid.NewGuid(), false, "encrypted");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Issuer not found");
    }

    [Fact]
    public async Task Handle_InvalidCardNumberLength_ReturnsError()
    {
        var issuer = CreateTestIssuer();
        _cardRepositoryMock.Setup(x => x.GetIssuerByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(issuer);

        var command = new CreateCardCommand(Guid.NewGuid(), "Test User", 12, 2030, "123", Guid.NewGuid(), false, "encrypted");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid card number length");
    }

    [Fact]
    public async Task Handle_DuplicateCard_ReturnsError()
    {
        var issuer = CreateTestIssuer();
        _cardRepositoryMock.Setup(x => x.GetIssuerByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(issuer);
        _cardRepositoryMock.Setup(x => x.HasDuplicateCardAsync(It.IsAny<Guid>(), It.IsAny<CardNetwork>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new CreateCardCommand(Guid.NewGuid(), "Test User", 12, 2030, "4111111111111111", Guid.NewGuid(), false, "encrypted");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Duplicate card");
    }

    [Fact]
    public async Task Handle_Success_CreatesCard()
    {
        var issuer = CreateTestIssuer();
        _cardRepositoryMock.Setup(x => x.GetIssuerByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(issuer);
        _cardRepositoryMock.Setup(x => x.HasDuplicateCardAsync(It.IsAny<Guid>(), It.IsAny<CardNetwork>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _cardRepositoryMock.Setup(x => x.AddAsync(It.IsAny<CreditCard>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new CreateCardCommand(Guid.NewGuid(), "Test User", 12, 2030, "4111111111111111", Guid.NewGuid(), false, "encrypted");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Card.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_SetDefault_UnsetsPreviousDefault()
    {
        var issuer = CreateTestIssuer();
        _cardRepositoryMock.Setup(x => x.GetIssuerByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(issuer);
        _cardRepositoryMock.Setup(x => x.HasDuplicateCardAsync(It.IsAny<Guid>(), It.IsAny<CardNetwork>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _cardRepositoryMock.Setup(x => x.AddAsync(It.IsAny<CreditCard>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new CreateCardCommand(Guid.NewGuid(), "Test User", 12, 2030, "4111111111111111", Guid.NewGuid(), true, "encrypted");

        var result = await _handler.Handle(command, CancellationToken.None);

        _cardRepositoryMock.Verify(x => x.UnsetDefaultForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static CardIssuer CreateTestIssuer() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test Bank",
        Network = CardNetwork.Visa,
        CreatedAtUtc = DateTime.UtcNow
    };
}