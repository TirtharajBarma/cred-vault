using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using NotificationService.Application.Interfaces;

namespace NotificationService.Infrastructure.Services;

public class GmailSmtpEmailSender : IEmailSender
{
    private readonly SmtpClient? _client;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly ILogger<GmailSmtpEmailSender> _logger;
    private readonly bool _configured;

    public GmailSmtpEmailSender(IConfiguration configuration, ILogger<GmailSmtpEmailSender> logger)
    {
        _logger = logger;
        _fromEmail = configuration["Gmail:FromEmail"] ?? configuration["Gmail:Username"] ?? "noreply@credvault.com";
        _fromName = configuration["Gmail:FromName"] ?? "CredVault";

        if (!string.IsNullOrEmpty(configuration["Gmail:AppPassword"]) && !string.IsNullOrEmpty(configuration["Gmail:Username"]))
        {
            _configured = true;
            try
            {
                _client = new SmtpClient();
                _client.Connect(configuration["Gmail:Host"], configuration.GetValue<int>("Gmail:Port", 587), SecureSocketOptions.StartTls);
                _client.Authenticate(configuration["Gmail:Username"], configuration["Gmail:AppPassword"]);
                _logger.LogInformation("SMTP connected successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SMTP connection failed");
                _client?.Dispose();
                _client = null;
                _configured = false;
            }
        }
        else
        {
            _configured = false;
            _logger.LogWarning("Gmail not configured - emails will be logged only (not sent)");
        }
    }

    public async Task<(bool Success, string? Error)> SendEmailAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        _logger.LogInformation("Email attempt: To={To}, Subject={Subject}", to, subject);

        if (!_configured)
        {
            _logger.LogWarning("Email NOT SENT (Gmail not configured): To={To}, Subject={Subject}", to, subject);
            return (false, "Gmail not configured - email simulated only");
        }

        try
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(_fromName, _fromEmail));
            msg.To.Add(MailboxAddress.Parse(to));
            msg.Subject = subject;
            msg.Body = new TextPart("html") { Text = body.Replace("\n", "<br>") };

            await _client!.SendAsync(msg, ct);
            _logger.LogInformation("Email SENT: To={To}, Subject={Subject}", to, subject);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email FAILED: To={To}, Subject={Subject}, Error={Error}", to, subject, ex.Message);
            return (false, ex.Message);
        }
    }

    public void Dispose()
    {
        if (_client != null)
        {
            _client.Disconnect(true);
            _client.Dispose();
        }
    }
}
