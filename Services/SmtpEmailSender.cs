using System.Net;
using System.Net.Mail;

namespace PmesCSharp.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        var host = _configuration["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
         _logger.LogWarning("SMTP not configured. Email to {Email} not sent. Subject: {Subject}. Body: {Body}", toEmail, subject, body);
            return;
        }

        var port = _configuration.GetValue<int?>("Smtp:Port") ?? 587;
        var from = _configuration["Smtp:From"] ?? "no-reply@pmes.local";
        var username = _configuration["Smtp:Username"];
        var password = _configuration["Smtp:Password"];
        var enableSsl = _configuration.GetValue<bool?>("Smtp:EnableSsl") ?? true;

        using var message = new MailMessage(from, toEmail)
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(username))		
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        // SmtpClient has no CancellationToken support.
        await client.SendMailAsync(message);
    }
}
