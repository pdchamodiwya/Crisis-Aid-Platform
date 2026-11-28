using System.Net;
using System.Net.Mail;
using System.Text;
using CrisisAid.Api.Domain;

namespace CrisisAid.Api.Services.Communication;

public class EmailNotificationProvider(
    IConfiguration config)
    : INotificationProvider
{
    public string Channel => "Email";

    public async Task<DeliveryResult> SendAsync(
        OutboxMessage message,
        UserAccount recipient,
        CancellationToken cancellationToken)
    {
        var host = config["SMTP:Host"];

        if (string.IsNullOrWhiteSpace(host))
        {
            return new DeliveryResult("NotConfigured");
        }

        using var smtp = new SmtpClient(
            host,
            config.GetValue<int>("SMTP:Port", 587))
        {
            EnableSsl = config.GetValue<bool>(
                "SMTP:EnableSsl", true)
        };

        var username = config["SMTP:Username"];

        if (!string.IsNullOrWhiteSpace(username))
        {
            smtp.Credentials = new NetworkCredential(
                username,
                config["SMTP:Password"]);
        }

        using var email = new MailMessage(
            config["SMTP:From"] ??
                "no-reply@example.org",
            recipient.Email,
            message.Subject,
            message.Body
        );

        email.SubjectEncoding = Encoding.UTF8;
        email.BodyEncoding = Encoding.UTF8;

        using var timeout =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        await smtp.SendMailAsync(email, timeout.Token);

        return new DeliveryResult("ProviderAccepted");
    }
}