using FluentAssertions;
using Xunit;
using NotificationService.Application.Commands;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;

namespace NotificationService.Tests.Commands;

public class ProcessNotificationCommandHandlerTests
{
    private readonly Mock<INotificationDbContext> _dbContextMock;
    private readonly Mock<IEmailSender> _emailSenderMock;
    private readonly Mock<ILogger<ProcessNotificationCommandHandler>> _loggerMock;
    private readonly ProcessNotificationCommandHandler _handler;

    public ProcessNotificationCommandHandlerTests()
    {
        _dbContextMock = new Mock<INotificationDbContext>();
        _emailSenderMock = new Mock<IEmailSender>();
        _loggerMock = new Mock<ILogger<ProcessNotificationCommandHandler>>();
        
        _handler = new ProcessNotificationCommandHandler(
            _dbContextMock.Object,
            _emailSenderMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_NoEmail_LogsNoEmailAndSaves()
    {
        var command = new ProcessNotificationCommand(
            "UserRegistered",
            null,
            "Test User",
            new { UserId = Guid.NewGuid() },
            null);

        _emailSenderMock.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, null));

        await _handler.Handle(command, CancellationToken.None);

        _dbContextMock.Verify(x => x.AddNotificationLog(It.Is<NotificationLog>(n => n.ErrorMessage == "No email provided")), Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmail_SendsEmail()
    {
        var command = new ProcessNotificationCommand(
            "UserRegistered",
            "test@example.com",
            "Test User",
            new { UserId = Guid.NewGuid() },
            null);

        _emailSenderMock.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, null));

        await _handler.Handle(command, CancellationToken.None);

        _emailSenderMock.Verify(x => x.SendEmailAsync("test@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _dbContextMock.Verify(x => x.AddNotificationLog(It.Is<NotificationLog>(n => n.IsSuccess)), Times.Once);
    }

    [Fact]
    public async Task Handle_EmailFails_LogsError()
    {
        var command = new ProcessNotificationCommand(
            "UserRegistered",
            "test@example.com",
            "Test User",
            new { UserId = Guid.NewGuid() },
            null);

        _emailSenderMock.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, "SMTP error"));

        await _handler.Handle(command, CancellationToken.None);

        _dbContextMock.Verify(x => x.AddNotificationLog(It.Is<NotificationLog>(n => !n.IsSuccess && n.ErrorMessage == "SMTP error")), Times.Once);
    }

    [Fact]
    public async Task Handle_UserRegisteredEvent_GeneratesWelcomeEmail()
    {
        var command = new ProcessNotificationCommand(
            "UserRegistered",
            "test@example.com",
            "John Doe",
            new { UserId = Guid.NewGuid() },
            null);

        _emailSenderMock.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, null));

        await _handler.Handle(command, CancellationToken.None);

        _emailSenderMock.Verify(x => x.SendEmailAsync(
            "test@example.com",
            "Welcome to CredVault",
            It.Is<string>(s => s.Contains("Welcome John Doe")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OtpEvent_GeneratesOtpEmail()
    {
        var command = new ProcessNotificationCommand(
            "UserOtpGenerated",
            "test@example.com",
            "Test User",
            new { UserId = Guid.NewGuid(), OtpCode = "123456", Purpose = "EmailVerification" },
            null);

        _emailSenderMock.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, null));

        await _handler.Handle(command, CancellationToken.None);

        _emailSenderMock.Verify(x => x.SendEmailAsync(
            "test@example.com",
            "Your Verification Code",
            It.Is<string>(s => s.Contains("123456")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}