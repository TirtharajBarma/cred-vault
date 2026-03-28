namespace NotificationService.Application.Interfaces;

public interface IEmailSender
{
    Task<(bool Success, string? Error)> SendEmailAsync(string to, string subject, string body, CancellationToken ct = default);
}
