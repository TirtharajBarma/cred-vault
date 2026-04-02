using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using MediatR;

namespace BillingService.Application.Queries.Statements;

public record GetMyStatementsQuery(Guid UserId) : IRequest<StatementsResult>;
public record GetStatementByIdQuery(Guid UserId, Guid StatementId) : IRequest<StatementDetailResult>;
public record GetStatementByBillIdQuery(Guid UserId, Guid BillId) : IRequest<StatementResult>;
public record GetAllStatementsQuery : IRequest<StatementsResult>;

public record StatementsResult(bool Success, List<StatementDto> Statements, string Message);
public record StatementDetailResult(bool Success, StatementDetailDto? Statement, string Message);

public record StatementDto(
    Guid Id,
    Guid? BillId,
    Guid CardId,
    string StatementPeriod,
    string CardLast4,
    string CardNetwork,
    string IssuerName,
    decimal ClosingBalance,
    decimal MinimumDue,
    decimal AmountPaid,
    StatementStatus Status,
    DateTime PeriodEndUtc,
    DateTime? DueDateUtc
);

public record StatementDetailDto(
    Guid Id,
    Guid UserId,
    Guid CardId,
    string StatementPeriod,
    string CardLast4,
    string CardNetwork,
    string IssuerName,
    decimal OpeningBalance,
    decimal TotalPurchases,
    decimal TotalPayments,
    decimal TotalRefunds,
    decimal PenaltyCharges,
    decimal InterestCharges,
    decimal ClosingBalance,
    decimal MinimumDue,
    decimal AmountPaid,
    StatementStatus Status,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    DateTime GeneratedAtUtc,
    DateTime? DueDateUtc,
    DateTime? PaidAtUtc,
    decimal CreditLimit,
    decimal AvailableCredit,
    List<StatementTransactionDto> Transactions
);

public record StatementTransactionDto(
    Guid Id,
    string Type,
    decimal Amount,
    string Description,
    DateTime DateUtc
);

public class GetMyStatementsQueryHandler(IStatementRepository statementRepository)
    : IRequestHandler<GetMyStatementsQuery, StatementsResult>
{
    public async Task<StatementsResult> Handle(GetMyStatementsQuery request, CancellationToken ct)
    {
        var statements = await statementRepository.GetByUserIdAsync(request.UserId, ct);
        var dtos = statements.Select(s => new StatementDto(
            s.Id, s.BillId, s.CardId, s.StatementPeriod, s.CardLast4, s.CardNetwork, s.IssuerName,
            s.ClosingBalance, s.MinimumDue, s.AmountPaid, s.Status, s.PeriodEndUtc, s.DueDateUtc
        )).ToList();

        return new StatementsResult(true, dtos, "Statements fetched successfully");
    }
}

public class GetStatementByIdQueryHandler(IStatementRepository statementRepository)
    : IRequestHandler<GetStatementByIdQuery, StatementDetailResult>
{
    public async Task<StatementDetailResult> Handle(GetStatementByIdQuery request, CancellationToken ct)
    {
        var statement = await statementRepository.GetByIdAsync(request.StatementId, ct);
        if (statement == null || statement.UserId != request.UserId)
        {
            return new StatementDetailResult(false, null, "Statement not found");
        }

        var transactions = await statementRepository.GetTransactionsAsync(request.StatementId, ct);
        var txnDtos = transactions.Select(t => new StatementTransactionDto(
            t.Id, t.Type, t.Amount, t.Description, t.DateUtc
        )).ToList();

        var detail = new StatementDetailDto(
            statement.Id, statement.UserId, statement.CardId, statement.StatementPeriod,
            statement.CardLast4, statement.CardNetwork, statement.IssuerName,
            statement.OpeningBalance, statement.TotalPurchases, statement.TotalPayments,
            statement.TotalRefunds, statement.PenaltyCharges, statement.InterestCharges,
            statement.ClosingBalance, statement.MinimumDue, statement.AmountPaid,
            statement.Status, statement.PeriodStartUtc, statement.PeriodEndUtc,
            statement.GeneratedAtUtc, statement.DueDateUtc, statement.PaidAtUtc,
            statement.CreditLimit, statement.AvailableCredit, txnDtos
        );

        return new StatementDetailResult(true, detail, "Statement details fetched successfully");
    }
}

public class GetStatementByBillIdQueryHandler(IStatementRepository statementRepository)
    : IRequestHandler<GetStatementByBillIdQuery, StatementResult>
{
    public async Task<StatementResult> Handle(GetStatementByBillIdQuery request, CancellationToken ct)
    {
        var statement = await statementRepository.GetByBillIdAsync(request.BillId, ct);
        if (statement == null || statement.UserId != request.UserId)
        {
            return new StatementResult(false, null!, "Statement not found for this bill");
        }

        var dto = new StatementDto(
            statement.Id, statement.BillId, statement.CardId, statement.StatementPeriod,
            statement.CardLast4, statement.CardNetwork, statement.IssuerName,
            statement.ClosingBalance, statement.MinimumDue, statement.AmountPaid,
            statement.Status, statement.PeriodEndUtc, statement.DueDateUtc
        );

        return new StatementResult(true, [dto], "Statement fetched successfully");
    }
}

public class GetAllStatementsQueryHandler(IStatementRepository statementRepository)
    : IRequestHandler<GetAllStatementsQuery, StatementsResult>
{
    public async Task<StatementsResult> Handle(GetAllStatementsQuery request, CancellationToken ct)
    {
        var statements = await statementRepository.GetAllAsync(ct);
        var dtos = statements.Select(s => new StatementDto(
            s.Id, s.BillId, s.CardId, s.StatementPeriod, s.CardLast4, s.CardNetwork, s.IssuerName,
            s.ClosingBalance, s.MinimumDue, s.AmountPaid, s.Status, s.PeriodEndUtc, s.DueDateUtc
        )).ToList();

        return new StatementsResult(true, dtos, "All statements fetched successfully");
    }
}
