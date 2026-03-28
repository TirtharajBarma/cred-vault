using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Enums;
using Shared.Contracts.Models;

namespace BillingService.Application.Commands.Bills;

public record MarkBillPaidCommand(Guid UserId, Guid BillId, decimal Amount) : IRequest<ApiResponse<Bill>>;

public class MarkBillPaidCommandHandler(
    IBillRepository billRepository,
    IRewardRepository rewardRepository,
    IUnitOfWork unitOfWork,
    ILogger<MarkBillPaidCommandHandler> logger)
    : IRequestHandler<MarkBillPaidCommand, ApiResponse<Bill>>
{
    public async Task<ApiResponse<Bill>> Handle(MarkBillPaidCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("MarkBillPaidCommand: BillId={BillId} UserId={UserId} Amount={Amount}", 
            request.BillId, request.UserId, request.Amount);
        
        var bill = await billRepository.GetByIdAndUserIdAsync(request.BillId, request.UserId, cancellationToken);

        if (bill is null)
        {
            logger.LogWarning("Bill not found: BillId={BillId} UserId={UserId}", request.BillId, request.UserId);
            return new ApiResponse<Bill> { Success = false, Message = "Bill not found." };
        }

        if (bill.Status == BillStatus.Paid)
        {
            return new ApiResponse<Bill> { Success = true, Message = "Bill is already paid.", Data = bill };
        }

        if (request.Amount < bill.MinDue)
        {
            return new ApiResponse<Bill> { Success = false, Message = $"Payment amount {request.Amount} is less than the minimum due {bill.MinDue}." };
        }

        var now = DateTime.UtcNow;
        bill.Status = BillStatus.Paid;
        bill.AmountPaid = request.Amount;
        bill.PaidAtUtc = now;
        bill.UpdatedAtUtc = now;

        // Rewards logic - use actual payment amount, not bill total
        var paymentAmount = request.Amount;
        var tier = await rewardRepository.GetBestMatchingTierAsync(bill.CardNetwork, bill.IssuerId, paymentAmount, now, cancellationToken);
        logger.LogInformation("Rewards: Bill={BillId} PaymentAmount={PaymentAmount} Network={Network} IssuerId={IssuerId} Tier={TierFound}", 
            bill.Id, paymentAmount, bill.CardNetwork, bill.IssuerId, tier is not null);
        if (tier is not null)
        {
            var account = await rewardRepository.GetAccountByUserIdAsync(request.UserId, cancellationToken);
            if (account is null)
            {
                account = new RewardAccount { Id = Guid.NewGuid(), UserId = request.UserId, RewardTierId = tier.Id, PointsBalance = 0, CreatedAtUtc = now, UpdatedAtUtc = now };
                await rewardRepository.AddAccountAsync(account, cancellationToken);
            }
            else
            {
                account.RewardTierId = tier.Id;
                account.UpdatedAtUtc = now;
                await rewardRepository.UpdateAccountAsync(account, cancellationToken);
            }

            var points = Math.Round(paymentAmount * tier.RewardRate, 2, MidpointRounding.AwayFromZero);
            if (points > 0)
            {
                account.PointsBalance += points;
                account.UpdatedAtUtc = now;

                await rewardRepository.AddTransactionAsync(new RewardTransaction { Id = Guid.NewGuid(), RewardAccountId = account.Id, BillId = bill.Id, Points = points, Type = RewardTransactionType.Earned, CreatedAtUtc = now }, cancellationToken);
            }
        }

        await billRepository.UpdateAsync(bill, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApiResponse<Bill> { Success = true, Message = "Bill marked as paid.", Data = bill };
    }
}
