using System.Net;
using System.Net.Mail;
using System.Text;

namespace PmesCSharp.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly IHostEnvironment _env;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger, IHostEnvironment env)
    {
        _configuration = configuration;
        _logger = logger;
       _env = env;
    }

    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        var host = _configuration["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
         _logger.LogError("SMTP not configured. Email to {Email} not sent. Subject: {Subject}. Body: {Body}", toEmail, subject, body);
            throw new InvalidOperationException("SMTP is not configured (Smtp:Host is empty). Set SMTP settings and restart the app.");
        }

        var port = _configuration.GetValue<int?>("Smtp:Port") ?? 587;
        var from = _configuration["Smtp:From"] ?? "no-reply@pmes.local";
        var username = _configuration["Smtp:Username"]?.Trim();
        var passwordRaw = _configuration["Smtp:Password"];
        var password = string.IsNullOrWhiteSpace(passwordRaw)
            ? null
            : new string(passwordRaw.Where(c => !char.IsWhiteSpace(c)).ToArray());
        var enableSsl = _configuration.GetValue<bool?>("Smtp:EnableSsl") ?? true;

        using var message = new MailMessage(from, toEmail)
        {
            Subject = subject,
            Body = body,
          SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false
        };

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        client.UseDefaultCredentials = false;

        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))		
        {
            client.Credentials = new NetworkCredential(username, password);
        }

     _logger.LogInformation("Sending email via SMTP {Host}:{Port} (SSL={EnableSsl}) From={From} To={To} Subject={Subject} Auth={HasAuth}", host, port, enableSsl, from, toEmail, subject, !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password));

        try
        {
            // SmtpClient has no CancellationToken support.
            await client.SendMailAsync(message);
            _logger.LogInformation("SMTP send completed. To={To} Subject={Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP send failed. Host={Host} Port={Port} From={From} To={To} Subject={Subject}", host, port, from, toEmail, subject);
            throw;
        }
    }
}
