using FluentAssertions;
using Xunit;
using PaymentService.Application.Commands.Payments;
using PaymentService.Domain.Enums;

namespace PaymentService.Tests.Commands.Payments;

public class InitiatePaymentCommandTests
{
    [Fact]
    public void InitiatePaymentCommand_ValidInput_CreatesCommand()
    {
        var userId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var billId = Guid.NewGuid();

        var command = new InitiatePaymentCommand(
            userId,
            cardId,
            billId,
            100m,
            PaymentType.Full,
            "Bearer token",
            10m
        );

        command.UserId.Should().Be(userId);
        command.CardId.Should().Be(cardId);
        command.BillId.Should().Be(billId);
        command.Amount.Should().Be(100m);
        command.PaymentType.Should().Be(PaymentType.Full);
        command.RewardsAmount.Should().Be(10m);
    }

    [Fact]
    public void InitiatePaymentCommand_WithNullRewards_CreatesCommand()
    {
        var command = new InitiatePaymentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            PaymentType.Partial,
            "Bearer token",
            null
        );

        command.RewardsAmount.Should().BeNull();
    }
}
