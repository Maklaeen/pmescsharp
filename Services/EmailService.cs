using System.Net;
using System.Net.Mail;

namespace PmesCSharp.Services;

public class EmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config) => _config = config;

    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        var host = _config["Smtp:Host"];
        var port = _config.GetValue<int>("Smtp:Port");
        var enableSsl = _config.GetValue<bool>("Smtp:EnableSsl");
        var from = _config["Smtp:From"];
        var username = _config["Smtp:Username"];
        var password = _config["Smtp:Password"];

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            Credentials = new NetworkCredential(username, password),
        };

        var message = new MailMessage
        {
            From = new MailAddress(from!, "PMES System"),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(toEmail);

        await client.SendMailAsync(message);
    }
}
