namespace CardService.Application.Interfaces;

public interface IBillingServiceClient
{
    Task<bool> HasPendingBillAsync(Guid cardId, CancellationToken cancellationToken = default);
}
