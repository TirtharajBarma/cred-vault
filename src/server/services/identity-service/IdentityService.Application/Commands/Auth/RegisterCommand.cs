using IdentityService.Application.DTOs.Responses;
using MediatR;

namespace IdentityService.Application.Commands.Auth;

public sealed record RegisterCommand(string FullName, string Email, string Password) : IRequest<AuthResult>;
