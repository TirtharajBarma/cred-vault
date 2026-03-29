using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Enums;
using Shared.Contracts.Models;

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
            return new ApiResponse<bool> { Success = false, Message = "Bill not found.", Data = false };
        }

        if (bill.Status != BillStatus.Paid)
        {
            logger.LogInformation("Bill {BillId} is not paid, nothing to revert", request.BillId);
            return new ApiResponse<bool> { Success = true, Message = "Bill is not paid.", Data = true };
        }

        // Revert bill status
        bill.Status = BillStatus.Pending;
        bill.AmountPaid = null;
        bill.PaidAtUtc = null;
        bill.UpdatedAtUtc = DateTime.UtcNow;

        await billRepository.UpdateAsync(bill, cancellationToken);

        // Deduct rewards - find the reward transaction for this bill and reverse it
        var rewardTx = await rewardRepository.GetTransactionByBillIdAsync(request.BillId, cancellationToken);
        if (rewardTx != null)
        {
            var account = await rewardRepository.GetAccountByUserIdAsync(request.UserId, cancellationToken);
            if (account != null && account.PointsBalance >= rewardTx.Points)
            {
                account.PointsBalance -= rewardTx.Points;
                account.UpdatedAtUtc = DateTime.UtcNow;
                await rewardRepository.UpdateAccountAsync(account, cancellationToken);

                // Mark the reward transaction as reversed
                rewardTx.Type = RewardTransactionType.Reversed;
                rewardTx.ReversedAtUtc = DateTime.UtcNow;
                await rewardRepository.UpdateTransactionAsync(rewardTx, cancellationToken);

                logger.LogInformation("Deducted {Points} points from user {UserId} account", rewardTx.Points, request.UserId);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApiResponse<bool> { Success = true, Message = "Bill reverted and rewards deducted.", Data = true };
    }
}
