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

public static class IdentityHelpers
{
    public static UserSummary ToUserSummary(IdentityUser user) =>
        new()
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            IsEmailVerified = user.IsEmailVerified,
            Status = ToApiStatus(user.Status),
            Role = ToApiRole(user.Role)
        };

    public static string GenerateAccessToken(IdentityUser user, JwtOptions options)
    {
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

    public static string GenerateOtpCode() => RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

    public static string ToApiRole(UserRole role) => role == UserRole.Admin ? "admin" : "user";

    public static string ToApiStatus(UserStatus status) => status switch
    {
        UserStatus.Active => "active",
        UserStatus.Suspended => "suspended",
        UserStatus.Blocked => "blocked",
        _ => "pending-verification"
    };

    public static bool TryParseStatus(string value, out UserStatus status)
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
