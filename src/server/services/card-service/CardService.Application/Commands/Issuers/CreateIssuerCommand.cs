using MediatR;
using CardService.Application.Abstractions.Persistence;
using CardService.Domain.Entities;
using Shared.Contracts.DTOs.Card.Requests;
using Shared.Contracts.DTOs.Card.Responses;
using Shared.Contracts.Enums;

namespace CardService.Application.Commands.Issuers;

public sealed record CreateIssuerCommand(
    string Name,
    string Network,
    bool IsActive
) : IRequest<IssuersResult>;

public sealed class CreateIssuerCommandHandler(
    ICardRepository cardRepository
) : IRequestHandler<CreateIssuerCommand, IssuersResult>
{
    public async Task<IssuersResult> Handle(CreateIssuerCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Network))
        {
            return new IssuersResult
            {
                Success = false,
                Message = "Name and Network are required."
            };
        }

        if (!TryParseNetwork(request.Network, out var network) || network == CardNetwork.Unknown)
        {
            return new IssuersResult
            {
                Success = false,
                Message = "Network must be Visa or Mastercard."
            };
        }

        var normalizedName = request.Name.Trim().ToLower();
        var exists = await cardRepository.HasDuplicateIssuerAsync(normalizedName, cancellationToken);

        if (exists)
        {
            return new IssuersResult
            {
                Success = false,
                Message = "An issuer with this name already exists."
            };
        }

        var now = DateTime.UtcNow;

        var issuer = new CardIssuer
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Network = network,
            IsActive = request.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await cardRepository.AddIssuerAsync(issuer, cancellationToken);

        return new IssuersResult
        {
            Success = true,
            Message = "Issuer created successfully.",
            Issuers =
            [
                new CardIssuerDto
                {
                    Id = issuer.Id,
                    Name = issuer.Name,
                    Network = issuer.Network.ToString(),
                    IsActive = issuer.IsActive,
                    CreatedAtUtc = issuer.CreatedAtUtc,
                    UpdatedAtUtc = issuer.UpdatedAtUtc
                }
            ]
        };
    }

    private static bool TryParseNetwork(string input, out CardNetwork network)
    {
        if (int.TryParse(input, out var networkInt) && Enum.IsDefined(typeof(CardNetwork), networkInt))
        {
            network = (CardNetwork)networkInt;
            return true;
        }

        return Enum.TryParse(input, ignoreCase: true, out network);
    }
}
