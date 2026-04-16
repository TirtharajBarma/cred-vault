using FluentAssertions;
using Xunit;
using BillingService.Application.Queries.Bills;
using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using Moq;
using Shared.Contracts.Models;

namespace BillingService.Tests.Queries.Bills;

public class GetMyBillsQueryTests
{
    private readonly Mock<IBillRepository> _billRepositoryMock;
    private readonly GetMyBillsQueryHandler _handler;

    public GetMyBillsQueryTests()
    {
        _billRepositoryMock = new Mock<IBillRepository>();
        _handler = new GetMyBillsQueryHandler(_billRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_NoBills_ReturnsEmptyList()
    {
        _billRepositoryMock.Setup(x => x.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Bill>());

        var query = new GetMyBillsQuery(Guid.NewGuid());

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithBills_ReturnsBills()
    {
        var bills = new List<Bill> { CreateTestBill(), CreateTestBill() };
        _billRepositoryMock.Setup(x => x.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bills);

        var query = new GetMyBillsQuery(Guid.NewGuid());

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);
    }

    private static Bill CreateTestBill() => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        CardId = Guid.NewGuid(),
        Amount = 500,
        Status = Domain.Entities.BillStatus.Pending,
        DueDateUtc = DateTime.UtcNow.AddDays(30)
    };
}