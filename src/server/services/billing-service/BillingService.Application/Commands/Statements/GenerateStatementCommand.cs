using System.Net.Http.Headers;
using System.Text.Json;
using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BillingService.Application.Commands.Statements;

public record GenerateStatementCommand(Guid CardId, Guid UserId, string AuthorizationHeader) : IRequest<StatementResult>;

public record StatementResult(bool Success, Guid? StatementId, string? Message);

public class GenerateStatementCommandHandler(
    IStatementRepository statementRepository,
    IBillRepository billRepository,
    IUnitOfWork unitOfWork,
    IHttpClientFactory httpClientFactory,
    Microsoft.Extensions.Configuration.IConfiguration configuration,
    ILogger<GenerateStatementCommandHandler> logger) : IRequestHandler<GenerateStatementCommand, StatementResult>
{
    public async Task<StatementResult> Handle(GenerateStatementCommand request, CancellationToken ct)
    {
        logger.LogInformation("GenerateStatement: CardId={CardId}, UserId={UserId}", request.CardId, request.UserId);

        var card = await FetchCardAsync(request.CardId, request.AuthorizationHeader, ct);
        if (card == null)
        {
            return new StatementResult(false, null, "Could not fetch card details");
        }

        if (card.UserId != request.UserId)
        {
            return new StatementResult(false, null, "Card does not belong to user");
        }

        var transactions = await FetchCardTransactionsAsync(request.CardId, request.AuthorizationHeader, ct);
        if (transactions == null || transactions.Count == 0)
        {
            return new StatementResult(false, null, "No transactions found for this card");
        }

        var periodStart = transactions.Min(t => t.dateUtc).Date;
        var periodEnd = DateTime.UtcNow.Date;

        var existingStatements = await statementRepository.GetByCardIdAsync(request.CardId, ct);
        if (existingStatements.Any(s => s.PeriodStartUtc == periodStart))
        {
            return new StatementResult(false, null, "Statement already exists for this period");
        }

        // type: 1=Purchase, 2=Payment, 3=Refund
        var purchases = transactions.Where(t => t.type == 1).ToList();
        var payments = transactions.Where(t => t.type == 2).ToList();
        var refunds = transactions.Where(t => t.type == 3).ToList();

        var totalPurchases = purchases.Sum(t => t.amount);
        var totalPayments = payments.Sum(t => t.amount);
        var totalRefunds = refunds.Sum(t => t.amount);

        // Get bills for this card to find penalty charges and link statement to bill
        var bills = await billRepository.GetByUserIdAsync(request.UserId, ct);
        var cardBills = bills.Where(b => b.CardId == request.CardId).ToList();
        var latestBill = cardBills.OrderByDescending(b => b.BillingDateUtc).FirstOrDefault();

        // Calculate penalty charges from overdue bills (3.5% of outstanding)
        var penaltyCharges = cardBills
            .Where(b => b.Status == BillStatus.Overdue)
            .Sum(b => Math.Round(b.Amount * 0.035m, 2, MidpointRounding.AwayFromZero));

        // Calculate interest charges (3.5% per month on outstanding balance)
        var interestCharges = 0m;
        if (latestBill != null && latestBill.Status != BillStatus.Paid)
        {
            var daysOverdue = (DateTime.UtcNow - latestBill.DueDateUtc).TotalDays;
            if (daysOverdue > 0)
            {
                var monthsOverdue = Math.Max(1, (int)Math.Ceiling(daysOverdue / 30));
                interestCharges = Math.Round(card.outstandingBalance * 0.035m * monthsOverdue, 2, MidpointRounding.AwayFromZero);
            }
        }

        var openingBalance = 0m;
        var closingBalance = totalPurchases + penaltyCharges + interestCharges - totalPayments - totalRefunds;
        var minDue = Math.Max(Math.Round(closingBalance * 0.10m, 2, MidpointRounding.AwayFromZero), 10.00m);

        // Determine statement status and payment info from latest bill
        var billStatus = latestBill?.Status ?? BillStatus.Pending;
        var statementStatus = billStatus switch
        {
            BillStatus.Paid => StatementStatus.Paid,
            BillStatus.Overdue => StatementStatus.Overdue,
            _ => StatementStatus.Generated
        };

        // If partial payment made, update status
        if (latestBill != null && latestBill.AmountPaid.HasValue && latestBill.AmountPaid > 0 && latestBill.AmountPaid < latestBill.Amount)
        {
            statementStatus = StatementStatus.PartiallyPaid;
        }

        var statement = new Statement
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            CardId = request.CardId,
            BillId = latestBill?.Id,
            StatementPeriod = $"{periodStart:MMM yyyy} - {periodEnd:MMM yyyy}",
            PeriodStartUtc = periodStart,
            PeriodEndUtc = periodEnd,
            GeneratedAtUtc = DateTime.UtcNow,
            DueDateUtc = latestBill?.DueDateUtc ?? periodEnd.AddDays(12),
            OpeningBalance = openingBalance,
            TotalPurchases = totalPurchases,
            TotalPayments = totalPayments,
            TotalRefunds = totalRefunds,
            PenaltyCharges = penaltyCharges,
            InterestCharges = interestCharges,
            ClosingBalance = closingBalance,
            MinimumDue = minDue,
            AmountPaid = latestBill?.AmountPaid ?? 0m,
            PaidAtUtc = latestBill?.PaidAtUtc,
            Status = statementStatus,
            CardLast4 = card.last4,
            CardNetwork = card.network,
            IssuerName = card.issuerName,
            CreditLimit = card.creditLimit,
            AvailableCredit = card.availableCredit,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await statementRepository.AddAsync(statement, ct);

        // Create statement transactions with source transaction IDs
        var statementTxns = new List<StatementTransaction>();
        var typeNames = new Dictionary<int, string> { { 1, "Purchase" }, { 2, "Payment" }, { 3, "Refund" } };
        
        foreach (var txn in transactions.OrderBy(t => t.dateUtc))
        {
            statementTxns.Add(new StatementTransaction
            {
                Id = Guid.NewGuid(),
                StatementId = statement.Id,
                SourceTransactionId = txn.Id,
                Type = typeNames.GetValueOrDefault(txn.type, "Unknown"),
                Amount = txn.amount,
                Description = txn.description,
                DateUtc = txn.dateUtc,
                CreatedAtUtc = DateTime.UtcNow
            });
        }
        await statementRepository.AddTransactionsAsync(statementTxns, ct);

        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Statement generated: StatementId={StatementId}, Period={Period}, BillId={BillId}", 
            statement.Id, statement.StatementPeriod, statement.BillId);
        return new StatementResult(true, statement.Id, "Statement generated successfully");
    }

    private async Task<CardDto?> FetchCardAsync(Guid cardId, string authHeader, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            var baseUrl = configuration["Services:CardService:BaseUrl"] ?? "http://localhost:5002";
            client.BaseAddress = new Uri(baseUrl);

            // Use the regular user endpoint since the card owner is making the request
            var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/cards/{cardId}");
            if (!string.IsNullOrEmpty(authHeader))
                request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);

            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ApiResponseWrapper<CardDto>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result?.Data;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception fetching card {CardId}", cardId);
            return null;
        }
    }

    private async Task<List<TransactionDto>?> FetchCardTransactionsAsync(Guid cardId, string authHeader, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            var baseUrl = configuration["Services:CardService:BaseUrl"] ?? "http://localhost:5002";
            client.BaseAddress = new Uri(baseUrl);

            var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/cards/{cardId}/transactions");
            if (!string.IsNullOrEmpty(authHeader))
                request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);

            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ApiResponseWrapper<List<TransactionDto>>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result?.Data;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception fetching transactions for card {CardId}", cardId);
            return null;
        }
    }

    private record ApiResponseWrapper<T>(bool Success, string Message, T? Data);
    public record CardDto(Guid Id, Guid UserId, string network, Guid issuerId, string issuerName, string last4, decimal creditLimit, decimal availableCredit, decimal outstandingBalance);
    public record TransactionDto(Guid Id, Guid CardId, Guid UserId, int type, decimal amount, string description, DateTime dateUtc);
    private record CardTransactionRef(Guid Id, int type, decimal amount, string description, DateTime dateUtc);
}
