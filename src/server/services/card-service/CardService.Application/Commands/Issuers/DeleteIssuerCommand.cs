using MediatR;
using CardService.Application.Abstractions.Persistence;
using CardService.Domain.Entities;

namespace CardService.Application.Commands.Issuers;

public sealed record DeleteIssuerCommand(Guid Id) : IRequest<DeleteIssuerResult>;

public sealed class DeleteIssuerCommandHandler(ICardRepository cardRepository) : IRequestHandler<DeleteIssuerCommand, DeleteIssuerResult>
{
    public async Task<DeleteIssuerResult> Handle(DeleteIssuerCommand request, CancellationToken cancellationToken)
    {
        var issuer = await cardRepository.GetIssuerByIdAsync(request.Id, cancellationToken);
        
        if (issuer == null || issuer.Id == Guid.Empty)
        {
            return new DeleteIssuerResult
            {
                Success = false,
                Message = "Issuer not found."
            };
        }

        if (await cardRepository.HasCardsByIssuerAsync(request.Id, cancellationToken))
        {
            return new DeleteIssuerResult
            {
                Success = false,
                Message = "Cannot delete issuer with attached cards. Remove or reassign all cards first."
            };
        }

        await cardRepository.DeleteIssuerAsync(issuer, cancellationToken);

        return new DeleteIssuerResult
        {
            Success = true,
            Message = "Issuer deleted successfully."
        };
    }
}

public sealed class DeleteIssuerResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}