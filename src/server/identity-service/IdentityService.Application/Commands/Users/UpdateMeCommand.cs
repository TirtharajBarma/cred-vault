using IdentityService.Application.DTOs.Responses;
using MediatR;

namespace IdentityService.Application.Commands.Users;

public sealed record UpdateMeCommand(Guid UserId, string FullName) : IRequest<UserResult>;
