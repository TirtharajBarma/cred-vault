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

    public GmailSmtpEmailSender(IConfiguration configuration, ILogger<GmailSmtpEmailSender> logger)
    {
        _logger = logger;
        var host = configuration["Gmail:Host"];
        var port = configuration.GetValue<int>("Gmail:Port", 587);
        var username = configuration["Gmail:Username"];
        var appPassword = configuration["Gmail:AppPassword"];
        _fromEmail = configuration["Gmail:FromEmail"] ?? username;
        _fromName = configuration["Gmail:FromName"] ?? "CredVault Notifications";

        if (!string.IsNullOrEmpty(host) && !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(appPassword))
        {
            try
            {
                _client = new SmtpClient();
                _client.Connect(host, port, SecureSocketOptions.StartTls);
                _client.Authenticate(username, appPassword);
                _logger.LogInformation("Gmail SMTP connected successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Gmail SMTP");
                _client?.Dispose();
                _client = null;
            }
        }
        else
        {
            _logger.LogWarning("Gmail configuration is incomplete. Email sender will only log emails to console.");
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
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_fromName, _fromEmail));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;

            message.Body = new TextPart("html")
            {
                Text = body.Replace("\n", "<br>")
            };

            await _client.SendAsync(message, ct);
            _logger.LogInformation("Email sent successfully to {To}", to);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
            return (false, ex.Message);
        }
    }

    public void Dispose()
    {
        _client?.Disconnect(true);
        _client?.Dispose();
    }
}
