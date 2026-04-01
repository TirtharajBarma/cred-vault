using CardService.Application.Abstractions.Persistence;
using CardService.Domain.Entities;
using Shared.Contracts.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CardService.Application.Commands.Cards;

public record ApplyStrikeCommand(Guid CardId, Guid BillId, string Reason) : IRequest<ApiResponse<ApplyStrikeResult>>;

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

        if (card.IsBlocked)
        {
            logger.LogInformation("Card {CardId} is already blocked", request.CardId);
            return new() { Success = false, Message = "Card is already blocked" };
        }

        var activeViolation = await violations.GetActiveViolationByCardIdAsync(request.CardId, ct);
        if (activeViolation != null)
        {
            logger.LogInformation("Card {CardId} already has active violation", request.CardId);
            return new() 
            { 
                Success = false, 
                Message = "Card already has an active violation for this billing cycle",
                Data = new ApplyStrikeResult(activeViolation.StrikeCount, card.IsBlocked, activeViolation.PenaltyAmount)
            };
        }

        var previousStrikes = card.StrikeCount;
        card.AddStrike();
        var newStrikeCount = card.StrikeCount;

        var penaltyAmount = CalculateInterest(card.OutstandingBalance, 1);
        
        card.OutstandingBalance += penaltyAmount;
        card.UpdatedAtUtc = DateTime.UtcNow;

        var violation = new CardViolation
        {
            Id = Guid.NewGuid(),
            CardId = card.Id,
            UserId = card.UserId,
            BillId = request.BillId,
            Type = ViolationType.LatePayment,
            StrikeCount = 1,
            Reason = request.Reason,
            PenaltyAmount = penaltyAmount,
            IsActive = true,
            AppliedAtUtc = DateTime.UtcNow
        };

        await violations.AddAsync(violation, ct);
        await cards.UpdateAsync(card, ct);

        logger.LogInformation(
            "Strike applied to Card {CardId}: PreviousStrikes={Previous}, NewStrikes={New}, IsBlocked={IsBlocked}, PenaltyApplied={Penalty}",
            card.Id, previousStrikes, newStrikeCount, card.IsBlocked, penaltyAmount);

        return new()
        {
            Success = true,
            Message = card.IsBlocked ? "Card blocked due to 3 strikes" : "Strike applied",
            Data = new ApplyStrikeResult(newStrikeCount, card.IsBlocked, penaltyAmount)
        };
    }

    private static decimal CalculateInterest(decimal amount, int months)
    {
        if (amount <= 0 || months <= 0)
            return 0;
        return Math.Round(amount * MonthlyInterestRate * months, 2);
    }
}
