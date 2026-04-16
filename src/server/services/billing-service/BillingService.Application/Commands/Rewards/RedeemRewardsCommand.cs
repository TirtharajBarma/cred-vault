using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Models;

namespace BillingService.Application.Commands.Rewards;

public record RedeemRewardsCommand(
    Guid UserId,
    int Points,
    RedeemRewardsTarget Target,         // where to apply
    Guid? TargetId                      // which bill
) : IRequest<ApiResponse<RedeemRewardsResult>>;

public enum RedeemRewardsTarget
{
    Account = 1,
    Bill = 2
}

// normal flow -> redeemRewardTarget = Bill


public class RedeemRewardsResult
{
    public decimal PointsRedeemed { get; set; }
    public decimal DollarValue { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? BillId { get; set; }
    public decimal NewPointsBalance { get; set; }
}

public class RedeemRewardsCommandHandler(
    IRewardRepository rewardRepository,
    IBillRepository billRepository,
    IStatementRepository statementRepository,
    IUnitOfWork unitOfWork,
    ILogger<RedeemRewardsCommandHandler> logger)
    : IRequestHandler<RedeemRewardsCommand, ApiResponse<RedeemRewardsResult>>
{
    private const decimal PointsToDollarRate = 0.25m;

    public async Task<ApiResponse<RedeemRewardsResult>> Handle(RedeemRewardsCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("RedeemRewardsCommand: UserId={UserId} Points={Points} Target={Target} TargetId={TargetId}",
            request.UserId, request.Points, request.Target, request.TargetId);

        if (request.Points <= 0)
        {
            return new ApiResponse<RedeemRewardsResult> { Success = false, Message = "Points must be greater than zero." };
        }

        var account = await rewardRepository.GetAccountByUserIdAsync(request.UserId, cancellationToken);
        if (account is null)
        {
            return new ApiResponse<RedeemRewardsResult> { Success = false, Message = "No reward account found." };
        }

        var dollarValue = request.Points * PointsToDollarRate;

        if (account.PointsBalance < request.Points)
        {
            // If insufficient points, use all available points and adjust the dollar value accordingly
            var availablePoints = (int)Math.Floor(account.PointsBalance);
            if (availablePoints <= 0)
            {
                logger.LogInformation("No points available for redemption. Proceeding with 0 points.");
                request = request with { Points = 0 };
                dollarValue = 0;
            }
            else
            {
                logger.LogInformation("Insufficient points for redemption: requested={Requested}, available={Available}. Using available points.",
                    request.Points, availablePoints);
            
                request = request with { Points = availablePoints };
                dollarValue = availablePoints * PointsToDollarRate;
            }
        }

        var now = DateTime.UtcNow;

        switch (request.Target)
        {
            case RedeemRewardsTarget.Account:
                return await HandleAccountRedemption(account, request.Points, dollarValue, now, cancellationToken);

            case RedeemRewardsTarget.Bill:
                if (!request.TargetId.HasValue)     // you must provide bill-Id
                {
                    return new ApiResponse<RedeemRewardsResult> { Success = false, Message = "Bill ID is required for bill redemption." };
                }
                return await HandleBillRedemption(account, request.TargetId.Value, request.Points, dollarValue, now, cancellationToken);

            default:
                return new ApiResponse<RedeemRewardsResult> { Success = false, Message = "Invalid redemption target." };
        }
    }

    private async Task<ApiResponse<RedeemRewardsResult>> HandleAccountRedemption(
        RewardAccount account,
        int points,
        decimal dollarValue,
        DateTime now,
        CancellationToken cancellationToken)
    {
        account.PointsBalance -= points;
        account.UpdatedAtUtc = now;

        await rewardRepository.UpdateAccountAsync(account, cancellationToken);

        await rewardRepository.AddTransactionAsync(new RewardTransaction
        {
            Id = Guid.NewGuid(),
            RewardAccountId = account.Id,
            BillId = null,                  // Account redemption - no bill associated
            Points = points,
            Type = RewardTransactionType.Redeemed,
            CreatedAtUtc = now
        }, cancellationToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save account redemption for UserId={UserId}", account.UserId);
            return new ApiResponse<RedeemRewardsResult>
            {
                Success = false,
                Message = $"Failed to process redemption: {ex.Message}"
            };
        }

        return new ApiResponse<RedeemRewardsResult>
        {
            Success = true,
            Message = $"Successfully redeemed {points} points for ${dollarValue:F2} account credit.",
            Data = new RedeemRewardsResult
            {
                PointsRedeemed = points,
                DollarValue = dollarValue,
                Message = $"${dollarValue:F2} applied to your account",
                NewPointsBalance = account.PointsBalance
            }
        };
    }

    private async Task<ApiResponse<RedeemRewardsResult>> HandleBillRedemption(
        RewardAccount account,
        Guid billId,
        int points,
        decimal dollarValue,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var bill = await billRepository.GetByIdAndUserIdAsync(billId, account.UserId, cancellationToken);
        if (bill is null)
        {
            return new ApiResponse<RedeemRewardsResult> { Success = false, Message = "Bill not found." };
        }

        if (bill.Status == BillStatus.Paid || bill.Status == BillStatus.Cancelled)
        {
            // Bill already paid via other means (e.g., SAGA), just deduct points and record transaction
            logger.LogInformation("Bill {BillId} already {Status}, deducting points only", billId, bill.Status);

            account.PointsBalance -= points;
            account.UpdatedAtUtc = now;

            await rewardRepository.UpdateAccountAsync(account, cancellationToken);

            await rewardRepository.AddTransactionAsync(new RewardTransaction
            {
                Id = Guid.NewGuid(),
                RewardAccountId = account.Id,
                BillId = billId,
                Points = points,
                Type = RewardTransactionType.Redeemed,
                CreatedAtUtc = now
            }, cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            var successMessage = $"Redeemed {points} points (${dollarValue:F2}) - bill already processed";
            return new ApiResponse<RedeemRewardsResult>
            {
                Success = true,
                Message = successMessage,
                Data = new RedeemRewardsResult
                {
                    PointsRedeemed = points,
                    DollarValue = dollarValue,
                    Message = successMessage,
                    BillId = billId,
                    NewPointsBalance = account.PointsBalance
                }
            };
        }

        // user pay will rewards
        var paymentAmount = dollarValue;
        var existingPaid = bill.AmountPaid ?? 0;
        var proposedTotalPaid = existingPaid + paymentAmount;
        var remainingBeforePayment = Math.Max(0, bill.Amount - existingPaid);

        if (remainingBeforePayment <= 0)
        {
            return new ApiResponse<RedeemRewardsResult> { Success = false, Message = "Bill is already fully paid." };
        }

        var finalPaidAmount = Math.Min(bill.Amount, proposedTotalPaid);
        var remainingAfterPayment = Math.Max(0, bill.Amount - finalPaidAmount);

        bill.AmountPaid = finalPaidAmount;
        bill.Status = remainingAfterPayment <= 0 ? BillStatus.Paid : BillStatus.PartiallyPaid;
        bill.PaidAtUtc = bill.Status == BillStatus.Paid ? now : null;
        bill.UpdatedAtUtc = now;

        account.PointsBalance -= points;
        account.UpdatedAtUtc = now;

        await billRepository.UpdateAsync(bill, cancellationToken);
        await rewardRepository.UpdateAccountAsync(account, cancellationToken);

        await rewardRepository.AddTransactionAsync(new RewardTransaction
        {
            Id = Guid.NewGuid(),
            RewardAccountId = account.Id,
            BillId = billId,
            Points = points,
            Type = RewardTransactionType.Redeemed,
            CreatedAtUtc = now
        }, cancellationToken);

        await EnsureStatementUpdatedAsync(bill, now, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var message = bill.Status == BillStatus.Paid
            ? $"Successfully redeemed {points} points (${dollarValue:F2}) to pay off this bill."
            : $"Successfully redeemed {points} points (${dollarValue:F2}). Remaining due: ${remainingAfterPayment:F2}";

        return new ApiResponse<RedeemRewardsResult>
        {
            Success = true,
            Message = message,
            Data = new RedeemRewardsResult
            {
                PointsRedeemed = points,
                DollarValue = dollarValue,
                Message = message,
                BillId = billId,
                NewPointsBalance = account.PointsBalance
            }
        };
    }

    private async Task EnsureStatementUpdatedAsync(Bill bill, DateTime now, CancellationToken cancellationToken)
    {
        var existing = await statementRepository.GetByBillIdAsync(bill.Id, cancellationToken);
        if (existing is null) return;

        existing.AmountPaid = bill.AmountPaid ?? 0;
        existing.TotalPayments = bill.AmountPaid ?? 0;
        existing.PaidAtUtc = bill.PaidAtUtc;
        existing.ClosingBalance = Math.Max(0, bill.Amount - (bill.AmountPaid ?? 0));
        existing.Status = bill.Status switch
        {
            BillStatus.Paid => StatementStatus.Paid,
            BillStatus.Overdue => StatementStatus.Overdue,
            BillStatus.PartiallyPaid => StatementStatus.PartiallyPaid,
            _ => existing.Status
        };
        existing.UpdatedAtUtc = now;
        await statementRepository.UpdateAsync(existing, cancellationToken);
    }
}
