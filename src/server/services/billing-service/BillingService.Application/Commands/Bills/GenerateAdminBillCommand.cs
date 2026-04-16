using System.Net.Http.Headers;
using System.Text.Json;
using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using MediatR;
using Shared.Contracts.Enums;
using Shared.Contracts.Models;
using Shared.Contracts.Events.Billing;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace BillingService.Application.Commands.Bills;

public record GenerateAdminBillCommand(Guid AdminUserId, Guid UserId, Guid CardId, string Currency, string AuthorizationHeader) : IRequest<ApiResponse<Bill>>;

public class GenerateAdminBillCommandHandler(
    IBillRepository bills,
    IStatementRepository statements,
    IHttpClientFactory http,
    Microsoft.Extensions.Configuration.IConfiguration config,       // read setting from appsetting.json [not to hardcode url]
    IUnitOfWork uow,                                                // wrapper around DBContext -> 1 DB call, Faster and safer
    IPublishEndpoint publisher,
    ILogger<GenerateAdminBillCommandHandler> logger)
    : IRequestHandler<GenerateAdminBillCommand, ApiResponse<Bill>>
{
    public async Task<ApiResponse<Bill>> Handle(GenerateAdminBillCommand request, CancellationToken ct)
    {
        logger.LogInformation("GenerateAdminBill requested: UserId={UserId}, CardId={CardId}", request.UserId, request.CardId);

        if (request.UserId == Guid.Empty || request.CardId == Guid.Empty)
        {
            logger.LogWarning("GenerateBill rejected: UserId or CardId is empty");
            return new() { Success = false, Message = "UserId and CardId are required." };
        }

        var (ok, card, err) = await GetCardAsync(request.CardId, request.AuthorizationHeader, ct);      // method return tuple
        if (!ok || card == null)
        {
            logger.LogWarning("Failed to fetch card {CardId}: {Error}", request.CardId, err);
            return new() { Success = false, Message = "Card not found" };
        }

        if (card.UserId != request.UserId)
        {
            logger.LogWarning("IDOR attempt: Card {CardId} belongs to {ActualUserId}, not {RequestUserId}",
                request.CardId, card.UserId, request.UserId);
            return new() { Success = false, Message = "Card does not belong to user" };
        }

        if (!Enum.TryParse<CardNetwork>(card.Network, true, out var network) || network == CardNetwork.Unknown)
        {
            logger.LogWarning("Invalid card network for {CardId}: {Network}", request.CardId, card.Network);
            return new() { Success = false, Message = "Invalid card network" };
        }

        if (card.OutstandingBalance <= 0)
        {
            logger.LogInformation("No balance to bill for CardId={CardId}", request.CardId);
            return new() { Success = false, Message = "No balance to bill" };
        }

        if (await bills.HasPendingBillAsync(request.UserId, request.CardId, ct))
        {
            logger.LogWarning("Duplicate bill attempt: UserId={UserId}, CardId={CardId}", request.UserId, request.CardId);
            return new() { Success = false, Message = "A pending bill already exists for this card" };
        }

        // min due calculate [10% or 10 which is higher]
        var minDue = Math.Max(Math.Round(card.OutstandingBalance * 0.10m, 2, MidpointRounding.AwayFromZero), 10.00m);       // calculate min due
        var now = DateTime.UtcNow;

        // calls a helper function -> return tuple
        var (billingDate, dueDate) = BillingCycleCalculator.CalculateBillingAndDueDate(card.BillingCycleStartDay);

        // create new bill
        var bill = new Bill
        {
            Id = Guid.NewGuid(), UserId = request.UserId, CardId = request.CardId,
            CardNetwork = network, IssuerId = card.IssuerId, Amount = card.OutstandingBalance,
            MinDue = minDue, Currency = request.Currency, BillingDateUtc = billingDate,
            DueDateUtc = dueDate, Status = BillStatus.Pending, CreatedAtUtc = now, UpdatedAtUtc = now
        };

        await bills.AddAsync(bill, ct);     // -> prepares data to save
        await EnsureStatementForBillAsync(bill, card, request.AuthorizationHeader, now, ct);
        await uow.SaveChangesAsync(ct);                                         //! actual save all DB changes at once
        logger.LogInformation("Bill created: {BillId}, Amount={Amount}, DueDate={DueDate}", bill.Id, bill.Amount, bill.DueDateUtc);

        // calls identify Services to get Email and Name : (_, -> ignore the value)
        //! NOTIFY other services that bill is generated
        var (userOk, user, _) = await GetUserAsync(request.UserId, request.AuthorizationHeader, ct);        //! header come from controller -> pass into another function
        var userEmail = userOk && user != null ? user.Email : null;     // if API success AND user exist -> use email
        var userName = userOk && user != null ? user.FullName : null;

        if (string.IsNullOrWhiteSpace(userEmail))
        {
            logger.LogWarning("Could not fetch user email for {UserId}. Publishing event with fallback.", request.UserId);
            userEmail = $"user-{request.UserId}@credvault.local";
            userName = "User";
        }

        await publisher.Publish<IBillGenerated>(new { BillId = bill.Id, UserId = bill.UserId, Email = userEmail, FullName = userName, CardId = bill.CardId, Amount = bill.Amount, DueDate = bill.DueDateUtc, GeneratedAt = bill.CreatedAtUtc }, ct);
        logger.LogInformation("Published IBillGenerated for {BillId}", bill.Id);

        return new() { Success = true, Message = "Bill generated", Data = bill };
    }

    // returns 3 things -> userOk, user, _ -> ignore message
    private async Task<(bool, CardDto?, string?)> GetCardAsync(Guid cardId, string auth, CancellationToken ct)
    {
        try
        {
            var client = http.CreateClient();   // used for external API
            client.BaseAddress = new Uri(config["Services:CardService:BaseUrl"] ?? "http://localhost:5002/");       // to use where not hardcode url
            
            var req = new HttpRequestMessage(HttpMethod.Get, $"api/v1/cards/admin/{cardId}");               // exact endpoints
            if (!string.IsNullOrEmpty(auth)) req.Headers.Authorization = AuthenticationHeaderValue.Parse(auth);     // attached JWT tokens
            var resp = await client.SendAsync(req, ct);      // built-in from HttpClient -> used to send HTTP request to another 
            
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Card service returned {Status} for {CardId}", resp.StatusCode, cardId);
                return (false, null, $"Card service error: {resp.StatusCode}");
            }
            var content = await resp.Content.ReadAsStringAsync(ct);         // converts Http response -> string

            // convert string to c# object
            var result = JsonSerializer.Deserialize<ApiResponse<CardDto>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result?.Data == null)
            {
                logger.LogWarning("Card service returned null data for {CardId}", cardId);
                return (false, null, "Card not found");
            }
            return (true, result.Data, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception fetching card {CardId}", cardId);
            return (false, null, "External service error");
        }
    }

    private async Task<(bool, UserDto?, string?)> GetUserAsync(Guid userId, string auth, CancellationToken ct)
    {
        try
        {
            var client = http.CreateClient();
            client.BaseAddress = new Uri(config["Services:IdentityService:BaseUrl"] ?? "http://localhost:5001/");
            
            var req = new HttpRequestMessage(HttpMethod.Get, $"api/v1/identity/users/{userId}");
            if (!string.IsNullOrEmpty(auth)) req.Headers.Authorization = AuthenticationHeaderValue.Parse(auth);
            
            var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return (false, null, $"User service error: {resp.StatusCode}");
            
            var content = await resp.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<UserResponseWrapper>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result?.Data?.User is null)
            {
                return (false, null, "User service returned empty payload");
            }
            return (result.Success, result.Data.User, result.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception fetching user {UserId}", userId);
            return (false, null, "External service error");
        }
    }

    private async Task<List<CardTransactionDto>> GetCardTransactionsAsync(Guid cardId, string auth, CancellationToken ct)
    {
        try
        {
            var client = http.CreateClient();
            client.BaseAddress = new Uri(config["Services:CardService:BaseUrl"] ?? "http://localhost:5002/");
            
            var req = new HttpRequestMessage(HttpMethod.Get, $"api/v1/cards/admin/{cardId}/transactions");
            if (!string.IsNullOrEmpty(auth)) req.Headers.Authorization = AuthenticationHeaderValue.Parse(auth);
            
            var resp = await client.SendAsync(req, ct);             // built-in from HttpClient -> used to send HTTP request to another service
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Card admin transactions API returned {Status} for {CardId}", resp.StatusCode, cardId);
                return [];
            }

            var content = await resp.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ApiResponse<List<CardTransactionDto>>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result?.Data ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception fetching admin card transactions for {CardId}", cardId);
            return [];
        }
    }

    private async Task EnsureStatementForBillAsync(Bill bill, CardDto card, string auth, DateTime now, CancellationToken ct)
    {
        var existing = await statements.GetByBillIdAsync(bill.Id, ct);
        
        // if statement exist STOP
        if (existing is not null)
        {
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
            TotalPayments = 0,
            TotalRefunds = 0,
            PenaltyCharges = 0,
            InterestCharges = 0,
            ClosingBalance = bill.Amount,
            MinimumDue = bill.MinDue,
            AmountPaid = 0,
            PaidAtUtc = null,
            Status = StatementStatus.Generated,
            CardLast4 = string.Empty,
            CardNetwork = card.Network,
            IssuerName = bill.IssuerId.ToString(),
            CreditLimit = card.CreditLimit,
            AvailableCredit = Math.Max(0, card.CreditLimit - card.OutstandingBalance),
            Notes = "Auto-generated when bill is generated",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        //! create Statement(empty -> find prev bills -> decide start date[lower bound] -> fetch transaction -> filter transaction -> attached to statement)

        await statements.AddAsync(statement, ct);

        var userBills = await bills.GetByUserIdAsync(bill.UserId, ct);          // find previous bills
        var previousBill = userBills                                            // last bills for same card
            .Where(b => b.CardId == bill.CardId && b.Id != bill.Id)
            .OrderByDescending(b => b.CreatedAtUtc)
            .FirstOrDefault();

        // Prefer transactions after the previous settled cycle to avoid leaking old items.
        var lowerBoundUtc = previousBill?.PaidAtUtc ?? previousBill?.CreatedAtUtc ?? bill.BillingDateUtc;

        var txns = await GetCardTransactionsAsync(bill.CardId, auth, ct);
        var lines = txns
            .Where(t => t.Type == 0 && t.DateUtc > lowerBoundUtc && t.DateUtc <= now)       // include purchases only
            .OrderBy(t => t.DateUtc)
            .Select(t => new StatementTransaction
            {
                Id = Guid.NewGuid(),
                StatementId = statement.Id,
                SourceTransactionId = t.Id,
                Type = t.Type switch                                        // convert enum [int] -> strings
                {
                    0 => "Purchase",
                    1 => "Payment",
                    2 => "Refund",
                    _ => "Unknown"
                },
                Amount = t.Amount,
                Description = t.Description,
                DateUtc = t.DateUtc,
                CreatedAtUtc = now
            })
            .ToList();

        if (lines.Count > 0)
        {
            await statements.AddTransactionsAsync(lines, ct);
        }
    }

    private record CardDto(Guid Id, Guid UserId, string Network, Guid IssuerId, decimal CreditLimit, decimal OutstandingBalance, int BillingCycleStartDay);
    private record UserDto(Guid Id, string Email, string FullName);
    private record CardTransactionDto(Guid Id, int Type, decimal Amount, string Description, DateTime DateUtc);

    private class UserResponseWrapper
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public UserPayload? Data { get; set; }
    }

    private class UserPayload
    {
        public UserDto? User { get; set; }
    }
}
