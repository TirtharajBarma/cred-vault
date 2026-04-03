using CardService.Application.Abstractions.Persistence;
using CardService.Domain.Entities;
using Shared.Contracts.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CardService.Application.Commands.Cards;

public record ApplyStrikeCommand(Guid CardId, Guid BillId, string Reason, int DaysOverdue) : IRequest<ApiResponse<ApplyStrikeResult>>;

public record ApplyStrikeResult(int NewStrikeCount, bool IsCardBlocked, decimal PenaltyApplied);

public class ApplyStrikeCommandHandler(ICardRepository cards, IViolationRepository violations, ILogger<ApplyStrikeCommandHandler> logger) : IRequestHandler<ApplyStrikeCommand, ApiResponse<ApplyStrikeResult>>
{
    private const decimal MonthlyInterestRate = 0.035m;

    public async Task<ApiResponse<ApplyStrikeResult>> Handle(ApplyStrikeCommand request, CancellationToken ct)
    {
        logger.LogInformation("ApplyStrike requested: CardId={CardId}, BillId={BillId}", request.CardId, request.BillId);

        var card = await cards.GetByIdAsync(request.CardId, ct);
        if (card == null)
        {
            logger.LogWarning("Card not found: {CardId}", request.CardId);
            return new() { Success = false, Message = "Card not found" };
        }

        var activeViolation = await violations.GetActiveViolationByCardIdAsync(request.CardId, ct);

        var desiredStrikeCount = Math.Min(3, Math.Max(1, ((Math.Max(1, request.DaysOverdue) - 1) / 12) + 1));

        if (card.StrikeCount >= desiredStrikeCount)
        {
            logger.LogInformation(
                "No strike escalation required for Card {CardId}. Current={Current}, Desired={Desired}",
                request.CardId, card.StrikeCount, desiredStrikeCount);

            return new() 
            { 
                Success = true,
                Message = card.IsBlocked ? "Card remains blocked due to strikes" : "No additional strike required",
                Data = new ApplyStrikeResult(card.StrikeCount, card.IsBlocked, activeViolation?.PenaltyAmount ?? 0)
            };
        }

        var previousStrikes = card.StrikeCount;
        var strikesToApply = desiredStrikeCount - card.StrikeCount;
        var totalPenaltyAmount = 0m;

        for (var i = 0; i < strikesToApply; i++)
        {
            var penaltyAmount = CalculateInterest(card.OutstandingBalance, 1);
            card.OutstandingBalance += penaltyAmount;
            totalPenaltyAmount += penaltyAmount;
            card.AddStrike();
        }

        var newStrikeCount = card.StrikeCount;

        card.UpdatedAtUtc = DateTime.UtcNow;

        if (activeViolation == null)
        {
            activeViolation = new CardViolation
            {
                Id = Guid.NewGuid(),
                CardId = card.Id,
                UserId = card.UserId,
                BillId = request.BillId,
                Type = ViolationType.LatePayment,
                StrikeCount = newStrikeCount,
                Reason = request.Reason,
                PenaltyAmount = totalPenaltyAmount,
                IsActive = true,
                AppliedAtUtc = DateTime.UtcNow
            };

            await violations.AddAsync(activeViolation, ct);
        }
        else
        {
            activeViolation.StrikeCount = newStrikeCount;
            activeViolation.PenaltyAmount += totalPenaltyAmount;
            activeViolation.Reason = request.Reason;
            activeViolation.AppliedAtUtc = DateTime.UtcNow;
            activeViolation.IsActive = !card.IsBlocked;

            await violations.UpdateAsync(activeViolation, ct);
        }

        await cards.UpdateAsync(card, ct);

        logger.LogInformation(
            "Strike applied to Card {CardId}: PreviousStrikes={Previous}, NewStrikes={New}, IsBlocked={IsBlocked}, PenaltyApplied={Penalty}",
            card.Id, previousStrikes, newStrikeCount, card.IsBlocked, totalPenaltyAmount);

        return new()
        {
            Success = true,
            Message = card.IsBlocked ? "Card blocked due to 3 strikes" : "Strike applied",
            Data = new ApplyStrikeResult(newStrikeCount, card.IsBlocked, totalPenaltyAmount)
        };
    }

    private static decimal CalculateInterest(decimal amount, int months)
    {
        if (amount <= 0 || months <= 0)
            return 0;
        return Math.Round(amount * MonthlyInterestRate * months, 2);
    }
}
