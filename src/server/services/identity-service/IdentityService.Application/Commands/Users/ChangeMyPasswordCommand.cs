using IdentityService.Application.DTOs.Responses;
using MediatR;

namespace IdentityService.Application.Commands.Users;

public sealed record ChangeMyPasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : IRequest<OperationResult>;
