using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Enums;
using Shared.Contracts.Models;
using Shared.Contracts.Exceptions;

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
            throw new NotFoundException("Bill", request.BillId);
        }

        if (bill.Status == BillStatus.Paid)
        {
            // prevent duplicate reward
            var alreadyProcessed = await rewardRepository.HasTransactionForBillAsync(request.BillId, cancellationToken);
            if (!alreadyProcessed)
            {
                // add rewards
                await EnsureRewardsRecordedAsync(bill, bill.AmountPaid ?? bill.Amount, now: DateTime.UtcNow, cancellationToken);
            }

            // async statement
            await EnsureStatementRecordedAsync(bill, 0m, DateTime.UtcNow, cancellationToken);
            return new ApiResponse<Bill> { Success = true, Message = "Bill is already paid.", Data = bill };
        }

        // user trying to pay 0 or negative value
        if (request.Amount <= 0)
        {
            throw new ValidationException("Payment amount must be greater than zero.");
        }

        var existingPaid = bill.AmountPaid ?? 0;        // does user paid anything...
        var proposedTotalPaid = existingPaid + request.Amount;
        var remainingBeforePayment = Math.Max(0, bill.Amount - existingPaid);

        // bill already setted
        if (remainingBeforePayment <= 0)
        {
            bill.Status = BillStatus.Paid;
            bill.PaidAtUtc = bill.PaidAtUtc ?? DateTime.UtcNow;
            await billRepository.UpdateAsync(bill, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new ApiResponse<Bill> { Success = true, Message = "Bill is already settled.", Data = bill };
        }

        // First payment on a bill must honor min due unless full amount is paid.
        //! Bill -> 1000, minDue -> 100, userPaid -> 50 : rejected
        if (existingPaid == 0 && request.Amount < bill.MinDue && request.Amount < remainingBeforePayment)
        {
            return new ApiResponse<Bill> { Success = false, Message = $"Payment amount {request.Amount} is less than the minimum due {bill.MinDue}." };
        }

        // After first partial payment, remaining amount must be cleared in full.
        if (existingPaid > 0 && request.Amount < remainingBeforePayment)
        {
            return new ApiResponse<Bill>
            {
                Success = false,
                Message = $"Minimum due was already paid once. Please pay the full remaining amount ({remainingBeforePayment:0.00})."
            };
        }

        var now = DateTime.UtcNow;
        var finalPaidAmount = Math.Min(bill.Amount, proposedTotalPaid);
        var paymentApplied = Math.Max(0m, finalPaidAmount - existingPaid);
        var remainingAfterPayment = Math.Max(0, bill.Amount - finalPaidAmount);

        bill.AmountPaid = finalPaidAmount;

        // this line decides if bill full payed or partially
        bill.Status = remainingAfterPayment <= 0 
                ? BillStatus.Paid 
                : BillStatus.PartiallyPaid;

        bill.PaidAtUtc = bill.Status == BillStatus.Paid ? now : null;
        bill.UpdatedAtUtc = now;

        await EnsureRewardsRecordedAsync(bill, finalPaidAmount, now, cancellationToken);
        await EnsureStatementRecordedAsync(bill, paymentApplied, now, cancellationToken);

        // Db updates happens
        await billRepository.UpdateAsync(bill, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var message = bill.Status == BillStatus.Paid
            ? "Bill marked as paid."
            : $"Partial payment recorded. Remaining due: {remainingAfterPayment:0.00}";

        return new ApiResponse<Bill> { Success = true, Message = message, Data = bill };
    }

    private async Task EnsureRewardsRecordedAsync(Bill bill, decimal paymentAmount, DateTime now, CancellationToken cancellationToken)
    {
        // Determine eligibility using total bill amount, then award points on cumulative paid amount.
        var tier = await rewardRepository.GetBestMatchingTierAsync(bill.CardNetwork, bill.IssuerId, bill.Amount, now, cancellationToken);
        if (tier is null)
        {
            logger.LogInformation("No matching reward tier for Bill={BillId}, Network={Network}, Issuer={IssuerId}", bill.Id, bill.CardNetwork, bill.IssuerId);
            return;
        }

        var targetPoints = Math.Round(paymentAmount * tier.RewardRate, 2, MidpointRounding.AwayFromZero);
        if (targetPoints < 0)
        {
            targetPoints = 0;
        }

        var account = await rewardRepository.GetAccountByUserIdAsync(bill.UserId, cancellationToken);
        var accountWasExisting = account is not null;

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

        var existingRewardTx = await rewardRepository.GetTransactionByBillIdAsync(bill.Id, cancellationToken);
        if (existingRewardTx is null)
        {
            if (targetPoints <= 0)
            {
                return;
            }

            account.PointsBalance += targetPoints;
            account.UpdatedAtUtc = now;

            if (accountWasExisting)
            {
                await rewardRepository.UpdateAccountAsync(account, cancellationToken);
            }

            await rewardRepository.AddTransactionAsync(new RewardTransaction
            {
                Id = Guid.NewGuid(),
                RewardAccountId = account.Id,
                BillId = bill.Id,
                Points = targetPoints,
                Type = RewardTransactionType.Earned,
                CreatedAtUtc = now
            }, cancellationToken);

            return;
        }

        var currentPoints = existingRewardTx.Type == RewardTransactionType.Earned ? existingRewardTx.Points : 0m;
        var delta = targetPoints - currentPoints;

        if (delta > 0)
        {
            account.PointsBalance += delta;
        }
        else if (delta < 0)
        {
            account.PointsBalance = Math.Max(0, account.PointsBalance + delta);
        }

        account.UpdatedAtUtc = now;
        await rewardRepository.UpdateAccountAsync(account, cancellationToken);

        existingRewardTx.Points = targetPoints;
        existingRewardTx.Type = targetPoints > 0 ? RewardTransactionType.Earned : RewardTransactionType.Reversed;
        existingRewardTx.ReversedAtUtc = targetPoints > 0 ? null : now;
        await rewardRepository.UpdateTransactionAsync(existingRewardTx, cancellationToken);
    }

    private async Task EnsureStatementRecordedAsync(Bill bill, decimal paymentApplied, DateTime now, CancellationToken cancellationToken)
    {
        var existing = await statementRepository.GetByBillIdAsync(bill.Id, cancellationToken);      // does statement  already exist for this bill
        if (existing is not null)
        {
            existing.AmountPaid = bill.AmountPaid ?? 0;     // copy paid amt
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

            if (paymentApplied > 0)
            {
                await statementRepository.AddTransactionsAsync(new List<StatementTransaction>
                {
                    new StatementTransaction
                    {
                        Id = Guid.NewGuid(),
                        StatementId = existing.Id,
                        Type = "Payment",
                        Amount = paymentApplied,
                        Description = "Bill payment",
                        DateUtc = now
                    }
                }, cancellationToken);
            }

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

        // Create statement transaction for the bill payment
        if (paymentApplied > 0)
        {
            await statementRepository.AddTransactionsAsync(new List<StatementTransaction>
            {
                new StatementTransaction
                {
                    Id = Guid.NewGuid(),
                    StatementId = statement.Id,
                    Type = "Payment",
                    Amount = paymentApplied,
                    Description = "Bill payment",
                    DateUtc = bill.PaidAtUtc ?? now
                }
            }, cancellationToken);
        }
    }
}
