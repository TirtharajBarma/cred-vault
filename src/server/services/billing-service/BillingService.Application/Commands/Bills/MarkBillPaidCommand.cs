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
    IStatementRepository statementRepository,
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
            var alreadyProcessed = await rewardRepository.HasTransactionForBillAsync(request.BillId, cancellationToken);
            if (!alreadyProcessed)
            {
                await EnsureRewardsRecordedAsync(bill, bill.AmountPaid ?? bill.Amount, now: DateTime.UtcNow, cancellationToken);
            }

            await EnsureStatementRecordedAsync(bill, DateTime.UtcNow, cancellationToken);
            return new ApiResponse<Bill> { Success = true, Message = "Bill is already paid.", Data = bill };
        }

        if (request.Amount <= 0)
        {
            return new ApiResponse<Bill> { Success = false, Message = "Payment amount must be greater than zero." };
        }

        var existingPaid = bill.AmountPaid ?? 0;
        var proposedTotalPaid = existingPaid + request.Amount;
        var remainingBeforePayment = Math.Max(0, bill.Amount - existingPaid);

        if (remainingBeforePayment <= 0)
        {
            bill.Status = BillStatus.Paid;
            bill.PaidAtUtc = bill.PaidAtUtc ?? DateTime.UtcNow;
            await billRepository.UpdateAsync(bill, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new ApiResponse<Bill> { Success = true, Message = "Bill is already settled.", Data = bill };
        }

        // First payment on a bill must honor min due unless full amount is paid.
        if (existingPaid == 0 && request.Amount < bill.MinDue && request.Amount < remainingBeforePayment)
        {
            return new ApiResponse<Bill> { Success = false, Message = $"Payment amount {request.Amount} is less than the minimum due {bill.MinDue}." };
        }

        var now = DateTime.UtcNow;
        var finalPaidAmount = Math.Min(bill.Amount, proposedTotalPaid);
        var remainingAfterPayment = Math.Max(0, bill.Amount - finalPaidAmount);

        bill.AmountPaid = finalPaidAmount;
        bill.Status = remainingAfterPayment <= 0 ? BillStatus.Paid : BillStatus.PartiallyPaid;
        bill.PaidAtUtc = bill.Status == BillStatus.Paid ? now : null;
        bill.UpdatedAtUtc = now;

        await EnsureRewardsRecordedAsync(bill, request.Amount, now, cancellationToken);
        await EnsureStatementRecordedAsync(bill, now, cancellationToken);

        await billRepository.UpdateAsync(bill, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var message = bill.Status == BillStatus.Paid
            ? "Bill marked as paid."
            : $"Partial payment recorded. Remaining due: {remainingAfterPayment:0.00}";

        return new ApiResponse<Bill> { Success = true, Message = message, Data = bill };
    }

    private async Task EnsureRewardsRecordedAsync(Bill bill, decimal paymentAmount, DateTime now, CancellationToken cancellationToken)
    {
        var alreadyProcessed = await rewardRepository.HasTransactionForBillAsync(bill.Id, cancellationToken);
        if (alreadyProcessed)
        {
            logger.LogInformation("Rewards already recorded for Bill={BillId}", bill.Id);
            return;
        }

        var tier = await rewardRepository.GetBestMatchingTierAsync(bill.CardNetwork, bill.IssuerId, paymentAmount, now, cancellationToken);
        if (tier is null)
        {
            logger.LogInformation("No matching reward tier for Bill={BillId}, Network={Network}, Issuer={IssuerId}", bill.Id, bill.CardNetwork, bill.IssuerId);
            return;
        }

        var account = await rewardRepository.GetAccountByUserIdAsync(bill.UserId, cancellationToken);
        if (account is null)
        {
            account = new RewardAccount
            {
                Id = Guid.NewGuid(),
                UserId = bill.UserId,
                RewardTierId = tier.Id,
                PointsBalance = 0,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            await rewardRepository.AddAccountAsync(account, cancellationToken);
        }
        else
        {
            account.RewardTierId = tier.Id;
            account.UpdatedAtUtc = now;
            await rewardRepository.UpdateAccountAsync(account, cancellationToken);
        }

        var points = Math.Round(paymentAmount * tier.RewardRate, 2, MidpointRounding.AwayFromZero);
        if (points <= 0)
        {
            return;
        }

        account.PointsBalance += points;
        account.UpdatedAtUtc = now;

        await rewardRepository.AddTransactionAsync(new RewardTransaction
        {
            Id = Guid.NewGuid(),
            RewardAccountId = account.Id,
            BillId = bill.Id,
            Points = points,
            Type = RewardTransactionType.Earned,
            CreatedAtUtc = now
        }, cancellationToken);
    }

    private async Task EnsureStatementRecordedAsync(Bill bill, DateTime now, CancellationToken cancellationToken)
    {
        var existing = await statementRepository.GetByBillIdAsync(bill.Id, cancellationToken);
        if (existing is not null)
        {
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
            return;
        }

        var statement = new Statement
        {
            Id = Guid.NewGuid(),
            UserId = bill.UserId,
            CardId = bill.CardId,
            BillId = bill.Id,
            StatementPeriod = $"{bill.BillingDateUtc:MMM yyyy}",
            PeriodStartUtc = bill.BillingDateUtc.Date,
            PeriodEndUtc = bill.DueDateUtc.Date,
            GeneratedAtUtc = now,
            DueDateUtc = bill.DueDateUtc,
            OpeningBalance = 0,
            TotalPurchases = bill.Amount,
            TotalPayments = bill.AmountPaid ?? 0,
            TotalRefunds = 0,
            PenaltyCharges = 0,
            InterestCharges = 0,
            ClosingBalance = Math.Max(0, bill.Amount - (bill.AmountPaid ?? 0)),
            MinimumDue = bill.MinDue,
            AmountPaid = bill.AmountPaid ?? 0,
            PaidAtUtc = bill.PaidAtUtc,
            Status = bill.Status switch
            {
                BillStatus.Paid => StatementStatus.Paid,
                BillStatus.Overdue => StatementStatus.Overdue,
                BillStatus.PartiallyPaid => StatementStatus.PartiallyPaid,
                _ => StatementStatus.Generated
            },
            CardLast4 = string.Empty,
            CardNetwork = bill.CardNetwork.ToString(),
            IssuerName = bill.IssuerId.ToString(),
            CreditLimit = 0,
            AvailableCredit = 0,
            Notes = "Auto-generated for paid bill",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await statementRepository.AddAsync(statement, cancellationToken);
    }
}
