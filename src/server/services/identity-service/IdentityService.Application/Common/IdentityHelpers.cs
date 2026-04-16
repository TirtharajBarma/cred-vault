using Shared.Contracts.Configuration;
using Shared.Contracts.DTOs.Identity.Responses;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Enums;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace IdentityService.Application.Common;

/// <summary>
/// Static helper class containing utility methods for identity operations.
/// Provides methods for JWT token generation, OTP code generation, and entity to DTO mapping.
/// All methods are stateless and thread-safe.
/// </summary>
public static class IdentityHelpers
{
    /// <summary>
    /// Converts IdentityUser entity to UserSummary DTO for API responses.
    /// Maps internal enum values (UserRole, UserStatus) to API-friendly strings.
    /// </summary>
    /// <param name="user">IdentityUser entity from database</param>
    /// <returns>UserSummary DTO with id, email, fullName, isEmailVerified, status, role</returns>
    public static UserSummary ToUserSummary(IdentityUser user) =>
        new()
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            IsEmailVerified = user.IsEmailVerified,
            Status = ToApiStatus(user.Status),      // covert enum to string
            Role = ToApiRole(user.Role)
        };

    /// <summary>
    /// Generates JWT access token for authenticated user.
    /// Creates token with claims: sub (userId), NameIdentifier (userId), Email, Role.
    /// Token expiration is configured via JwtOptions.AccessTokenMinutes.
    /// </summary>
    /// <param name="user">IdentityUser entity to generate token for</param>
    /// <param name="options">JWT configuration (SecretKey, Issuer, Audience, AccessTokenMinutes)</param>
    /// <returns>Signed JWT token string</returns>
    public static string GenerateAccessToken(IdentityUser user, JwtOptions options)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>        // create claims
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, ToApiRole(user.Role))
        };

        var token = new JwtSecurityToken(       // build actual jwt
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(options.AccessTokenMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token); // convert to string
    }

    /// <summary>
    /// Generates a 6-digit OTP code for email verification or password reset.
    /// Uses cryptographically secure RandomNumberGenerator (not Math.Random).
    /// </summary>
    /// <returns>6-digit string between 100000-999999</returns>
    public static string GenerateOtpCode() => RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

    /// <summary>
    ///! Converts internal UserRole enum to API-friendly string.
    /// </summary>
    /// <param name="role">Internal UserRole (Admin or User)</param>
    /// <returns>String: "admin" or "user"</returns>
    public static string ToApiRole(UserRole role) => role == UserRole.Admin ? "admin" : "user";

    /// <summary>
    /// Converts internal UserStatus enum to API-friendly string.
    /// </summary>
    /// <param name="status">Internal UserStatus (Active, Suspended, Blocked, PendingVerification)</param>
    /// <returns>String: "active", "suspended", "blocked", or "pending-verification"</returns>
    public static string ToApiStatus(UserStatus status) => status switch
    {
        UserStatus.Active => "active",
        UserStatus.Suspended => "suspended",
        UserStatus.Blocked => "blocked",
        _ => "pending-verification"
    };
}
