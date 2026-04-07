using MediatR;
using CardService.Application.Abstractions.Persistence;
using Shared.Contracts.DTOs.Card.Responses;
using Shared.Contracts.Enums;

namespace CardService.Application.Commands.Issuers;

public sealed record UpdateIssuerCommand(
    Guid Id,
    string Name,
    string Network
) : IRequest<IssuersResult>;

public sealed class UpdateIssuerCommandHandler(
    ICardRepository cardRepository
) : IRequestHandler<UpdateIssuerCommand, IssuersResult>
{
    public async Task<IssuersResult> Handle(UpdateIssuerCommand request, CancellationToken cancellationToken)
    {
        var issuer = await cardRepository.GetIssuerByIdAsync(request.Id, cancellationToken);
        
        if (issuer == null)
        {
            return new IssuersResult
            {
                Success = false,
                Message = "Issuer not found."
            };
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return new IssuersResult
            {
                Success = false,
                Message = "Name is required."
            };
        }

        Enum.TryParse<CardNetwork>(request.Network, ignoreCase: true, out var network);

        issuer.Name = request.Name.Trim();
        issuer.Network = network;
        issuer.UpdatedAtUtc = DateTime.UtcNow;

        await cardRepository.UpdateIssuerAsync(issuer, cancellationToken);

        return new IssuersResult
        {
            Success = true,
            Message = "Issuer updated successfully.",
            Issuers = new List<CardIssuerDto>
            {
                new CardIssuerDto
                {
                    Id = issuer.Id,
                    Name = issuer.Name,
                    Network = (int)issuer.Network,
                    CreatedAtUtc = issuer.CreatedAtUtc,
                    UpdatedAtUtc = issuer.UpdatedAtUtc
                }
            }
        };
    }
}
