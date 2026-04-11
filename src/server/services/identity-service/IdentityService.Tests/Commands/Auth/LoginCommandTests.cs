using FluentAssertions;
using Xunit;
using IdentityService.Application.Commands.Auth;
using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Enums;
using Microsoft.Extensions.Options;
using Moq;
using Shared.Contracts.Configuration;
using Shared.Contracts.DTOs.Identity.Responses;

namespace IdentityService.Tests.Commands.Auth;

public class LoginCommandTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly IOptions<JwtOptions> _jwtOptions;
    private readonly LoginCommandHandler _handler;

    public LoginCommandTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        var jwtOptions = new JwtOptions
        {
            SecretKey = "test-secret-key-that-is-at-least-32-characters-long",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessTokenMinutes = 60
        };
        _jwtOptions = Options.Create(jwtOptions);
        _handler = new LoginCommandHandler(_userRepositoryMock.Object, _jwtOptions);
    }

    [Fact]
    public async Task Handle_EmptyEmail_ReturnsValidationError()
    {
        var command = new LoginCommand("", "password123");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationError);
    }

    [Fact]
    public async Task Handle_EmptyPassword_ReturnsValidationError()
    {
        var command = new LoginCommand("test@example.com", "");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationError);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsInvalidCredentials()
    {
        _userRepositoryMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdentityUser?)null);

        var command = new LoginCommand("notfound@example.com", "password123");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.InvalidCredentials);
    }

    [Fact]
    public async Task Handle_WrongPassword_ReturnsInvalidCredentials()
    {
        var user = CreateTestUser();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("correctpassword");
        _userRepositoryMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new LoginCommand("test@example.com", "wrongpassword");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.InvalidCredentials);
    }

    [Fact]
    public async Task Handle_InactiveUser_ReturnsAccountLocked()
    {
        var user = CreateTestUser();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123");
        user.Status = UserStatus.Suspended;
        _userRepositoryMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new LoginCommand("test@example.com", "password123");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.AccountLocked);
    }

    [Fact]
    public async Task Handle_Success_ReturnsToken()
    {
        var user = CreateTestUser();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123");
        user.Status = UserStatus.Active;
        _userRepositoryMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new LoginCommand("test@example.com", "password123");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.AccessToken.Should().NotBeNullOrEmpty();
    }

    private static IdentityUser CreateTestUser() => new()
    {
        Id = Guid.NewGuid(),
        Email = "test@example.com",
        FullName = "Test User",
        Status = UserStatus.Active,
        Role = UserRole.User
    };
}