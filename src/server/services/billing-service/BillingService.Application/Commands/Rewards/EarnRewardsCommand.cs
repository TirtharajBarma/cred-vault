using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Enums;
using Shared.Contracts.Models;

namespace BillingService.Application.Commands.Rewards;

public record EarnRewardsCommand(
    Guid UserId,
    decimal AmountPaid,
    Guid BillId,
    Guid CardId,
    CardNetwork CardNetwork
) : IRequest<ApiResponse<EarnRewardsResult>>;

public class EarnRewardsResult
{
    public decimal PointsEarned { get; set; }
    public decimal DollarValue { get; set; }
    public string Message { get; set; } = string.Empty;
    public decimal NewPointsBalance { get; set; }
}

public class EarnRewardsCommandHandler(
    IRewardRepository rewardRepository,
    IUnitOfWork unitOfWork,
    ILogger<EarnRewardsCommandHandler> logger
) : IRequestHandler<EarnRewardsCommand, ApiResponse<EarnRewardsResult>>
{
    public async Task<ApiResponse<EarnRewardsResult>> Handle(EarnRewardsCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("EarnRewardsCommand: UserId={UserId}, AmountPaid={AmountPaid}, BillId={BillId}",
            request.UserId, request.AmountPaid, request.BillId);

        if (request.AmountPaid <= 0)
        {
            return new ApiResponse<EarnRewardsResult> { Success = false, Message = "Amount must be greater than zero." };
        }

        var account = await rewardRepository.GetAccountByUserIdAsync(request.UserId, cancellationToken);
        if (account is null)
        {
            logger.LogWarning("No reward account found for user {UserId}, creating one", request.UserId);
            account = new RewardAccount
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                PointsBalance = 0,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            await rewardRepository.AddAccountAsync(account, cancellationToken);
        }

        var tiers = await rewardRepository.GetTiersAsync(cancellationToken);
        var effectiveTier = tiers
            .Where(t => t.CardNetwork == request.CardNetwork || t.CardNetwork == CardNetwork.Unknown)
            .Where(t => !t.EffectiveToUtc.HasValue || t.EffectiveToUtc.Value > DateTime.UtcNow)
            .OrderByDescending(t => t.MinSpend)
            .FirstOrDefault();
        
        var pointsPerDollar = effectiveTier?.RewardRate ?? 1.0m;  // Default: 1 point per dollar (using RewardRate field)
        
        var pointsEarned = Math.Floor(request.AmountPaid * pointsPerDollar);
        if (pointsEarned <= 0)
        {
            return new ApiResponse<EarnRewardsResult>
            {
                Success = true,
                Message = "No points earned for this transaction.",
                Data = new EarnRewardsResult
                {
                    PointsEarned = 0,
                    DollarValue = 0,
                    Message = "No points earned",
                    NewPointsBalance = account.PointsBalance
                }
            };
        }

        account.PointsBalance += pointsEarned;
        account.UpdatedAtUtc = DateTime.UtcNow;

        await rewardRepository.UpdateAccountAsync(account, cancellationToken);

        await rewardRepository.AddTransactionAsync(new RewardTransaction
        {
            Id = Guid.NewGuid(),
            RewardAccountId = account.Id,
            BillId = request.BillId,
            Points = pointsEarned,
            Type = RewardTransactionType.Earned,
            CreatedAtUtc = DateTime.UtcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Earned {Points} points for user {UserId}. New balance: {Balance}",
            pointsEarned, request.UserId, account.PointsBalance);

        return new ApiResponse<EarnRewardsResult>
        {
            Success = true,
            Message = $"Earned {pointsEarned} points!",
            Data = new EarnRewardsResult
            {
                PointsEarned = pointsEarned,
                DollarValue = pointsEarned * 0.25m,
                Message = $"You earned {pointsEarned} points",
                NewPointsBalance = account.PointsBalance
            }
        };
    }
}