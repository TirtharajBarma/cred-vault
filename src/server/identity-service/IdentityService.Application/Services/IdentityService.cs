using IdentityService.Application.Abstractions.Notifications;
using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.Configuration;
using IdentityService.Application.DTOs.Requests;
using IdentityService.Application.DTOs.Responses;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Enums;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace IdentityService.Application.Services;

public sealed class IdentityService(
    IUserRepository userRepository,
    IEmailSender emailSender,
    IOptions<JwtOptions> jwtOptions) : IIdentityService
{
    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.FullName))
        {
            return new AuthResult
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "FullName, Email and Password are required."
            };
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var existing = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            return new AuthResult
            {
                Success = false,
                ErrorCode = ErrorCodes.DuplicateEmail,
                Message = "Email already registered."
            };
        }

        var user = new IdentityUser
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            FullName = request.FullName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsEmailVerified = false,
            Status = UserStatus.PendingVerification,
            Role = UserRole.User,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await userRepository.AddAsync(user, cancellationToken);

        var accessToken = GenerateAccessToken(user);

        return new AuthResult
        {
            Success = true,
            Message = "Registration successful. Please verify your email.",
            AccessToken = accessToken,
            User = ToUserSummary(user)
        };
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return new AuthResult
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "Email and Password are required."
            };
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return new AuthResult
            {
                Success = false,
                ErrorCode = ErrorCodes.InvalidCredentials,
                Message = "Invalid email or password."
            };
        }

        if (user.Status is not UserStatus.Active and not UserStatus.PendingVerification)
        {
            return new AuthResult
            {
                Success = false,
                ErrorCode = ErrorCodes.Forbidden,
                Message = $"User status is {ToApiStatus(user.Status)}. Login not allowed."
            };
        }

        var accessToken = GenerateAccessToken(user);

        return new AuthResult
        {
            Success = true,
            Message = "Login successful.",
            AccessToken = accessToken,
            User = ToUserSummary(user)
        };
    }

    public async Task<OperationResult> ResendVerificationAsync(ResendVerificationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return new OperationResult
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "Email is required."
            };
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null)
        {
            return new OperationResult
            {
                Success = false,
                ErrorCode = ErrorCodes.UserNotFound,
                Message = "No account exists with this email."
            };
        }

        if (user.IsEmailVerified)
        {
            return new OperationResult
            {
                Success = true,
                Message = "Email is already verified."
            };
        }

        var otp = GenerateOtpCode();
        user.EmailVerificationOtp = otp;
        user.EmailVerificationOtpExpiresAtUtc = DateTime.UtcNow.AddMinutes(10);
        user.UpdatedAtUtc = DateTime.UtcNow;
        await userRepository.UpdateAsync(user, cancellationToken);

        try
        {
            await emailSender.SendAsync(
                user.Email,
                "CredVault verification code",
                $"Your verification OTP is {otp}. It will expire in 10 minutes.",
                cancellationToken);
        }
        catch
        {
            return new OperationResult
            {
                Success = false,
                ErrorCode = ErrorCodes.EmailSendFailed,
                Message = "Failed to send verification email. Check SMTP configuration."
            };
        }

        return new OperationResult
        {
            Success = true,
            Message = "Verification email sent."
        };
    }

    public async Task<OperationResult> VerifyEmailOtpAsync(VerifyEmailOtpRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Otp))
        {
            return new OperationResult
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "Email and OTP are required."
            };
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null)
        {
            return new OperationResult
            {
                Success = false,
                ErrorCode = ErrorCodes.UserNotFound,
                Message = "No account exists with this email."
            };
        }

        if (user.IsEmailVerified)
        {
            return new OperationResult
            {
                Success = true,
                Message = "Email is already verified."
            };
        }

        if (string.IsNullOrWhiteSpace(user.EmailVerificationOtp) ||
            user.EmailVerificationOtpExpiresAtUtc is null)
        {
            return new OperationResult
            {
                Success = false,
                ErrorCode = ErrorCodes.InvalidOtp,
                Message = "OTP not found. Please request a new OTP."
            };
        }

        if (user.EmailVerificationOtpExpiresAtUtc <= DateTime.UtcNow)
        {
            return new OperationResult
            {
                Success = false,
                ErrorCode = ErrorCodes.OtpExpired,
                Message = "OTP expired. Please request a new OTP."
            };
        }

        if (!string.Equals(user.EmailVerificationOtp, request.Otp.Trim(), StringComparison.Ordinal))
        {
            return new OperationResult
            {
                Success = false,
                ErrorCode = ErrorCodes.InvalidOtp,
                Message = "Invalid OTP."
            };
        }

        user.IsEmailVerified = true;
        user.Status = UserStatus.Active;
        user.EmailVerificationOtp = null;
        user.EmailVerificationOtpExpiresAtUtc = null;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await userRepository.UpdateAsync(user, cancellationToken);

        return new OperationResult
        {
            Success = true,
            Message = "Email verified successfully."
        };
    }

    public async Task<UserResult> GetMeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        return user is null
            ? new UserResult { Success = false, ErrorCode = ErrorCodes.UserNotFound, Message = "User not found." }
            : new UserResult { Success = true, Message = "User profile fetched.", User = ToUserSummary(user) };
    }

    public async Task<UserResult> UpdateMeAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return new UserResult { Success = false, ErrorCode = ErrorCodes.UserNotFound, Message = "User not found." };
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return new UserResult { Success = false, ErrorCode = ErrorCodes.ValidationError, Message = "FullName is required." };
        }

        user.FullName = request.FullName.Trim();
        user.UpdatedAtUtc = DateTime.UtcNow;
        await userRepository.UpdateAsync(user, cancellationToken);

        return new UserResult { Success = true, Message = "Profile updated.", User = ToUserSummary(user) };
    }

    public async Task<OperationResult> ChangeMyPasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return new OperationResult { Success = false, ErrorCode = ErrorCodes.UserNotFound, Message = "User not found." };
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return new OperationResult { Success = false, ErrorCode = ErrorCodes.InvalidCredentials, Message = "Current password is incorrect." };
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return new OperationResult { Success = false, ErrorCode = ErrorCodes.ValidationError, Message = "New password must be at least 8 characters." };
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAtUtc = DateTime.UtcNow;
        await userRepository.UpdateAsync(user, cancellationToken);

        return new OperationResult { Success = true, Message = "Password changed successfully." };
    }

    public async Task<UserResult> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        return user is null
            ? new UserResult { Success = false, ErrorCode = ErrorCodes.UserNotFound, Message = "User not found." }
            : new UserResult { Success = true, Message = "User fetched.", User = ToUserSummary(user) };
    }

    public async Task<UserResult> UpdateUserStatusAsync(Guid userId, UpdateUserStatusRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryParseStatus(request.Status, out var status))
        {
            return new UserResult
            {
                Success = false,
                ErrorCode = ErrorCodes.InvalidStatus,
                Message = "Invalid status. Use active, suspended, blocked, pending-verification."
            };
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return new UserResult { Success = false, ErrorCode = ErrorCodes.UserNotFound, Message = "User not found." };
        }

        user.Status = status;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await userRepository.UpdateAsync(user, cancellationToken);

        return new UserResult { Success = true, Message = "User status updated.", User = ToUserSummary(user) };
    }

    private static UserSummary ToUserSummary(IdentityUser user) =>
        new()
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            IsEmailVerified = user.IsEmailVerified,
            Status = ToApiStatus(user.Status),
            Role = ToApiRole(user.Role)
        };

    private string GenerateAccessToken(IdentityUser user)
    {
        var options = jwtOptions.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, ToApiRole(user.Role))
        };

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(options.AccessTokenMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateOtpCode() => RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

    private static string ToApiRole(UserRole role) => role == UserRole.Admin ? "admin" : "user";

    private static string ToApiStatus(UserStatus status) => status switch
    {
        UserStatus.Active => "active",
        UserStatus.Suspended => "suspended",
        UserStatus.Blocked => "blocked",
        _ => "pending-verification"
    };

    private static bool TryParseStatus(string value, out UserStatus status)
    {
        var normalized = value.Trim().ToLowerInvariant();
        status = normalized switch
        {
            "active" => UserStatus.Active,
            "suspended" => UserStatus.Suspended,
            "blocked" => UserStatus.Blocked,
            "pending-verification" => UserStatus.PendingVerification,
            "pendingverification" => UserStatus.PendingVerification,
            _ => UserStatus.PendingVerification
        };

        return normalized is "active" or "suspended" or "blocked" or "pending-verification" or "pendingverification";
    }
}
