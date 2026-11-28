using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CrisisAid.Api.Data;
using CrisisAid.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrisisAid.Api.Services.Communication;

public class SmsNotificationProvider(
    AidDbContext db,
    IConfiguration config,
    IHttpClientFactory clients)
    : INotificationProvider
{
    public string Channel => "SMS";

    public async Task<DeliveryResult> SendAsync(
        OutboxMessage message,
        UserAccount recipient,
        CancellationToken cancellationToken)
    {
        if (!config.GetValue<bool>("SMS:Enabled"))
        {
            return new DeliveryResult("NotConfigured");
        }

        var preference =
            await db.CommunicationPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.UserId == recipient.Id,
                    cancellationToken);

        if (preference == null ||
            !preference.SmsEnabled ||
            !preference.PhoneVerified ||
            string.IsNullOrWhiteSpace(
                preference.PhoneNumber))
        {
            return new DeliveryResult("Skipped");
        }

        var sid = config["SMS:AccountSid"];
        var token = config["SMS:AuthToken"];
        var from = config["SMS:From"];

        if (string.IsNullOrWhiteSpace(sid) ||
            string.IsNullOrWhiteSpace(token) ||
            string.IsNullOrWhiteSpace(from))
        {
            return new DeliveryResult("NotConfigured");
        }

        var url =
            $"https://api.twilio.com/2010-04-01/" +
            $"Accounts/{sid}/Messages.json";

        using var request =
            new HttpRequestMessage(HttpMethod.Post, url);

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{sid}:{token}"));

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Basic", credentials);

        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["From"] = from,
                ["To"] = preference.PhoneNumber,
                ["Body"] = message.Body
            });

        var client = clients.CreateClient("sms");

        using var response =
            await client.SendAsync(
                request, cancellationToken);

        response.EnsureSuccessStatusCode();

        var body =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        using var json = JsonDocument.Parse(body);

        var messageId =
            json.RootElement.GetProperty("sid")
                .GetString();

        var status =
            config.GetValue<bool>("SMS:TestMode", true)
                ? "Simulated"
                : "ProviderAccepted";

        return new DeliveryResult(status, messageId);
    }
}