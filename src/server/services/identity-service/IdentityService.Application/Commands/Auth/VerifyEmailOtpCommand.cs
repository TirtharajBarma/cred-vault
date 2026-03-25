using IdentityService.Application.DTOs.Responses;
using MediatR;

namespace IdentityService.Application.Commands.Auth;

public sealed record VerifyEmailOtpCommand(string Email, string Otp) : IRequest<OperationResult>;
