using IdentityService.Application.Abstractions.Notifications;
using IdentityService.Application.Configuration;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace IdentityService.Infrastructure.Notifications;

public sealed class SmtpEmailSender(IOptions<EmailOptions> emailOptions) : IEmailSender
{
    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        var options = emailOptions.Value;
        using var message = new MailMessage
        {
            From = new MailAddress(options.SenderEmail, options.SenderName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(toEmail));

        using var client = new SmtpClient(options.Host, options.Port)
        {
            EnableSsl = options.UseSsl,
            Credentials = new NetworkCredential(options.Username, options.Password)
        };

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
    }
}
