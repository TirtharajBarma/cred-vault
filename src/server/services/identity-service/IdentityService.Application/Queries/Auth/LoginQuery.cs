using IdentityService.Application.DTOs.Responses;
using MediatR;

namespace IdentityService.Application.Queries.Auth;

public sealed record LoginQuery(string Email, string Password) : IRequest<AuthResult>;
