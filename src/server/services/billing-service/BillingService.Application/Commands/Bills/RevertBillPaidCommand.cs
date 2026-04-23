using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Enums;
using Shared.Contracts.Models;
using Shared.Contracts.Exceptions;

namespace BillingService.Application.Commands.Bills;

public record RevertBillPaidCommand(Guid UserId, Guid BillId, decimal Amount) : IRequest<ApiResponse<bool>>;

public class RevertBillPaidCommandHandler(
    IBillRepository billRepository,
    IRewardRepository rewardRepository,
    IUnitOfWork unitOfWork,
    ILogger<RevertBillPaidCommandHandler> logger)
    : IRequestHandler<RevertBillPaidCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(RevertBillPaidCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("RevertBillPaidCommand: BillId={BillId} UserId={UserId} Amount={Amount}", 
            request.BillId, request.UserId, request.Amount);
        
        var bill = await billRepository.GetByIdAndUserIdAsync(request.BillId, request.UserId, cancellationToken);

        if (bill is null)
        {
            logger.LogWarning("Bill not found: BillId={BillId} UserId={UserId}", request.BillId, request.UserId);
            throw new NotFoundException("Bill", request.BillId);
        }

        if (bill.Status == BillStatus.Pending || bill.Status == BillStatus.Cancelled)
        {
            logger.LogInformation("Bill {BillId} is {Status}, nothing to revert", request.BillId, bill.Status);
            return new ApiResponse<bool> { Success = true, Message = "Bill is not paid.", Data = true };
        }

        var now = DateTime.UtcNow;

        // Revert bill status - handle both Paid and PartiallyPaid
        var currentPaid = bill.AmountPaid ?? 0;
        var revertAmount = Math.Max(0, request.Amount);
        var newPaid = Math.Max(0, currentPaid - revertAmount);      // new amt to revert

        // Recompute rewards based on remaining paid amount after this revert.
        // get reward transaction for this bill
        var rewardTx = await rewardRepository.GetTransactionByBillIdAsync(request.BillId, cancellationToken);
        if (rewardTx != null)
        {
            // get user's account
            var account = await rewardRepository.GetAccountByUserIdAsync(request.UserId, cancellationToken);
            if (account != null)
            {
                // get reward's rate
                var tier = await rewardRepository.GetBestMatchingTierAsync(
                    bill.CardNetwork,
                    bill.IssuerId,
                    bill.Amount,
                    now,
                    cancellationToken);

                var targetPoints = 0m;

                // rewards based on remaining paid amt..... paid: 1000, reward: 20 | revert: 300 -> paid: 700, reward: 14
                if (tier is not null && newPaid > 0)
                {
                    targetPoints = Math.Round(newPaid * tier.RewardRate, 2, MidpointRounding.AwayFromZero);
                }

                // only count if earned
                var currentPoints = rewardTx.Type == RewardTransactionType.Earned ? rewardTx.Points : 0m;
                var pointsToSubtract = Math.Max(0m, currentPoints - targetPoints);
                var pointsToAddBack = Math.Max(0m, targetPoints - currentPoints);

                if (pointsToSubtract > 0)
                {
                    account.PointsBalance = Math.Max(0, account.PointsBalance - pointsToSubtract);
                }
                else if (pointsToAddBack > 0)
                {
                    account.PointsBalance += pointsToAddBack;
                }

                account.UpdatedAtUtc = now;
                await rewardRepository.UpdateAccountAsync(account, cancellationToken);

                rewardTx.Points = targetPoints;
                rewardTx.Type = targetPoints > 0 ? RewardTransactionType.Earned : RewardTransactionType.Reversed;
                rewardTx.ReversedAtUtc = targetPoints > 0 ? null : now;
                await rewardRepository.UpdateTransactionAsync(rewardTx, cancellationToken);

                logger.LogInformation("Adjusted reward points for Bill={BillId}, UserId={UserId}: before={CurrentPoints}, after={TargetPoints}",
                    request.BillId, request.UserId, currentPoints, targetPoints);
            }
        }

        if (bill.Status == BillStatus.Paid && revertAmount >= currentPaid)
        {
            // Full reversal
            bill.Status = BillStatus.Pending;
            bill.AmountPaid = null;
            bill.PaidAtUtc = null;
        }
        else
        {
            // Partial reversal
            bill.AmountPaid = newPaid > 0 ? newPaid : null;
            bill.Status = newPaid > 0 ? BillStatus.PartiallyPaid : BillStatus.Pending;
            bill.PaidAtUtc = newPaid > 0 ? bill.PaidAtUtc : null;
        }

        bill.UpdatedAtUtc = now;
        await billRepository.UpdateAsync(bill, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApiResponse<bool> { Success = true, Message = "Bill reverted and rewards deducted.", Data = true };
    }
}
