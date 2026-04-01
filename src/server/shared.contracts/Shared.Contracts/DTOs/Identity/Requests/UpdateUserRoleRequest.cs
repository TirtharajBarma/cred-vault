namespace Shared.Contracts.DTOs.Identity.Requests;

public sealed class UpdateUserRoleRequest
{
    public string Role { get; set; } = string.Empty;
}
