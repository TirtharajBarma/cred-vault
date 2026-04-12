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

        // Revert rewards - find the reward transaction for this bill and reverse it
        // rewards transaction obj
        var rewardTx = await rewardRepository.GetTransactionByBillIdAsync(request.BillId, cancellationToken);
        if (rewardTx != null)
        {
            var account = await rewardRepository.GetAccountByUserIdAsync(request.UserId, cancellationToken);
            if (account != null)
            {
                // Prevent balance from going negative
                if (account.PointsBalance >= rewardTx.Points)
                {
                    account.PointsBalance -= rewardTx.Points;
                }
                else
                {
                    account.PointsBalance = 0;
                }
                account.UpdatedAtUtc = DateTime.UtcNow;
                await rewardRepository.UpdateAccountAsync(account, cancellationToken);

                // Mark the reward transaction as reversed
                rewardTx.Type = RewardTransactionType.Reversed;
                rewardTx.ReversedAtUtc = DateTime.UtcNow;
                await rewardRepository.UpdateTransactionAsync(rewardTx, cancellationToken);

                logger.LogInformation("Reversed {Points} points from user {UserId} account", rewardTx.Points, request.UserId);
            }
        }

        // Revert bill status - handle both Paid and PartiallyPaid
        var currentPaid = bill.AmountPaid ?? 0;
        var revertAmount = request.Amount;

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
            var newPaid = Math.Max(0, currentPaid - revertAmount);
            bill.AmountPaid = newPaid > 0 ? newPaid : null;
            bill.Status = newPaid > 0 ? BillStatus.PartiallyPaid : BillStatus.Pending;
            bill.PaidAtUtc = newPaid > 0 ? bill.PaidAtUtc : null;
        }

        bill.UpdatedAtUtc = DateTime.UtcNow;
        await billRepository.UpdateAsync(bill, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApiResponse<bool> { Success = true, Message = "Bill reverted and rewards deducted.", Data = true };
    }
}
