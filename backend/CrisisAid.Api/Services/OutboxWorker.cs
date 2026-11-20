using System.Net;
using System.Net.Mail;
using CrisisAid.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CrisisAid.Api.Services;

// Transactional outbox: notifications and event keys are saved alongside business changes.
// Local in-app delivery always works; SMTP email is optional and failures are retried.
public class OutboxWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<OutboxWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await FlushAsync(stoppingToken); }
            catch (Exception e) { log.LogError(e, "Outbox dispatch iteration failed"); }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
    private async Task FlushAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AidDbContext>();
        var rows = await db.Outbox.Where(x => x.Status == "Pending" &&
            (x.NextAttemptAt == null || x.NextAttemptAt <= DateTime.UtcNow))
            .OrderBy(x => x.CreatedAt).Take(15).ToListAsync(ct);
        foreach (var row in rows)
        {
            var recipient = await db.Users.FindAsync([row.RecipientId], ct);
            var host = config["SMTP:Host"];
            if (string.IsNullOrWhiteSpace(host) || recipient == null)
            {
                row.Status = "InAppOnly"; // not misrepresented as a sent email
                continue;
            }
            try
            {
                using var smtp = new SmtpClient(host, int.TryParse(config["SMTP:Port"], out var p) ? p : 587)
                {
                    EnableSsl = true, Credentials = new NetworkCredential(config["SMTP:Username"], config["SMTP:Password"]),
                    Timeout = 5000
                };
                using var mail = new MailMessage(config["SMTP:From"] ?? "no-reply@example.org", recipient.Email, row.Subject, row.Body);
                await smtp.SendMailAsync(mail, ct);
                row.Status = "Sent";
            }
            catch (Exception ex) when (ex is SmtpException or InvalidOperationException or OperationCanceledException)
            {
                row.Attempts++;
                row.LastError = ex.GetType().Name;
                row.NextAttemptAt = DateTime.UtcNow.AddMinutes(Math.Min(60, Math.Pow(2, row.Attempts)));
                if (row.Attempts >= 5) row.Status = "Failed";
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
