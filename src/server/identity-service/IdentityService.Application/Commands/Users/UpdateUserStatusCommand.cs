using IdentityService.Application.DTOs.Responses;
using MediatR;

namespace IdentityService.Application.Commands.Users;

public sealed record UpdateUserStatusCommand(Guid UserId, string Status) : IRequest<UserResult>;
