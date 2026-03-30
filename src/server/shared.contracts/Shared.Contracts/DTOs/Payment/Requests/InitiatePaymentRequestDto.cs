namespace Shared.Contracts.DTOs.Payment.Requests;

public sealed class InitiatePaymentRequestDto
{
    public Guid CardId { get; set; }
    public Guid BillId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentType { get; set; } = string.Empty;
}
