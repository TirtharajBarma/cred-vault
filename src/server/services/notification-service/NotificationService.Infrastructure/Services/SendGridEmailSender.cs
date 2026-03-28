using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Interfaces;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace NotificationService.Infrastructure.Services;

public class SendGridEmailSender : IEmailSender
{
    private readonly ISendGridClient? _client;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly ILogger<SendGridEmailSender> _logger;

    public SendGridEmailSender(IConfiguration configuration, ILogger<SendGridEmailSender> logger)
    {
        _logger = logger;
        var apiKey = configuration["SendGrid:ApiKey"];
        _fromEmail = configuration["SendGrid:FromEmail"] ?? "no-reply@credvault.com";
        _fromName = configuration["SendGrid:FromName"] ?? "CredVault Notifications";

        if (!string.IsNullOrEmpty(apiKey))
        {
            _client = new SendGridClient(apiKey);
        }
        else
        {
            _logger.LogWarning("SendGrid ApiKey is missing. Email sender will only log emails to console.");
        }
    }

    public async Task<(bool Success, string? Error)> SendEmailAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        if (_client == null)
        {
            _logger.LogInformation("SIMULATED EMAIL to {To}: {Subject}\n{Body}", to, subject, body);
            return (true, null);
        }

        try
        {
            var msg = new SendGridMessage()
            {
                From = new EmailAddress(_fromEmail, _fromName),
                Subject = subject,
                PlainTextContent = body,
                HtmlContent = body // In this simple case, we use body for both
            };
            msg.AddTo(new EmailAddress(to));

            var response = await _client.SendEmailAsync(msg, ct);

            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var error = await response.Body.ReadAsStringAsync(ct);
            _logger.LogError("Failed to send email to {To}. Status: {Status}. Error: {Error}", to, response.StatusCode, error);
            return (false, $"SendGrid error: {response.StatusCode} - {error}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending email to {To}", to);
            return (false, ex.Message);
        }
    }
}
