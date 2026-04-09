using Shared.Contracts.Enums;

namespace Shared.Contracts.DTOs.Card.Responses;

public class CardTransactionDto
{
    public Guid Id { get; set; }
    public Guid CardId { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime DateUtc { get; set; }
}
