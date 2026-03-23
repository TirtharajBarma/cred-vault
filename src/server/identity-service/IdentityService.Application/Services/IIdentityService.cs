using IdentityService.Application.DTOs.Requests;
using IdentityService.Application.DTOs.Responses;

namespace IdentityService.Application.Services;

public interface IIdentityService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> ResendVerificationAsync(ResendVerificationRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> VerifyEmailOtpAsync(VerifyEmailOtpRequest request, CancellationToken cancellationToken = default);
    Task<UserResult> GetMeAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserResult> UpdateMeAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> ChangeMyPasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task<UserResult> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserResult> UpdateUserStatusAsync(Guid userId, UpdateUserStatusRequest request, CancellationToken cancellationToken = default);
}
