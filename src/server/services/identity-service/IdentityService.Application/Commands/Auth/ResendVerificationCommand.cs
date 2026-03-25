using IdentityService.Application.DTOs.Responses;
using MediatR;

namespace IdentityService.Application.Commands.Auth;

public sealed record ResendVerificationCommand(string Email) : IRequest<OperationResult>;
