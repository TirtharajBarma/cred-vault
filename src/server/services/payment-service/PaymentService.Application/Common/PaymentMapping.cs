using Shared.Contracts.DTOs.Payment.Responses;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Common;

public static class PaymentMapping
{
    public static PaymentDto ToDto(Payment p) => new()
    {
        Id            = p.Id,
        UserId        = p.UserId,
        CardId        = p.CardId,
        BillId        = p.BillId,
        Amount        = p.Amount,
        PaymentType   = p.PaymentType.ToString(),
        Status        = p.Status.ToString(),
        FailureReason = p.FailureReason,
        CreatedAtUtc  = p.CreatedAtUtc,
        UpdatedAtUtc  = p.UpdatedAtUtc
    };

    public static TransactionDto ToDto(Transaction t) => new()
    {
        Id           = t.Id,
        PaymentId    = t.PaymentId,
        UserId       = t.UserId,
        Amount       = t.Amount,
        Type         = t.Type.ToString(),
        Description  = t.Description,
        CreatedAtUtc = t.CreatedAtUtc
    };

    public static RiskScoreDto ToDto(RiskScore r) => new()
    {
        Id           = r.Id,
        PaymentId    = r.PaymentId,
        UserId       = r.UserId,
        Score        = r.Score,
        Decision     = r.Decision.ToString(),
        CreatedAtUtc = r.CreatedAtUtc
    };
}
