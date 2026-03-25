using IdentityService.Application.DTOs.Responses;
using MediatR;

namespace IdentityService.Application.Queries.Users;

public sealed record GetUserByIdQuery(Guid UserId) : IRequest<UserResult>;
