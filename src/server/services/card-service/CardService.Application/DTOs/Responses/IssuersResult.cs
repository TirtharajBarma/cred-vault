using Shared.Contracts.Models;

namespace CardService.Application.DTOs.Responses;

public sealed class IssuersResult : OperationResult
{
    public List<IssuerDto> Issuers { get; set; } = [];
}

public sealed class IssuerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Network { get; set; } = string.Empty;
}
