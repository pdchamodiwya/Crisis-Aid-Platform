using CrisisAid.Api.Data;
using CrisisAid.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrisisAid.Api.Services;

public class DonationFraudService(
    AidDbContext db,
    IConfiguration config)
{
    public async Task<List<string>> DetectAsync(
        Donation donation)
    {
        var reasons = new List<string>();

        if (!string.IsNullOrWhiteSpace(
            donation.TransactionReference))
        {
            var reference = donation
                .TransactionReference.Trim().ToUpper();

            if (await db.Donations.AnyAsync(x =>
                x.Id != donation.Id &&
                x.Status == "Confirmed" &&
                x.TransactionReference != null &&
                x.TransactionReference.ToUpper() == reference))
            {
                reasons.Add("Duplicate transaction reference");
            }
        }

        var limit = config.GetValue<decimal>(
            "Donations:ReviewThresholdLkr", 100000);

        if (donation.Kind == "Monetary" &&
            donation.Currency == "LKR" &&
            donation.Amount >= limit)
        {
            reasons.Add(
                "Donation exceeds configured review threshold");
        }

        var since = DateTime.UtcNow.AddMinutes(-10);

        var similar = await db.Donations.CountAsync(x =>
            x.Id != donation.Id &&
            x.DonorId == donation.DonorId &&
            x.CampaignId == donation.CampaignId &&
            x.Currency == donation.Currency &&
            x.Kind == "Monetary" &&
            x.Amount == donation.Amount &&
            x.At >= since);

        if (donation.Kind == "Monetary" && similar >= 2)
        {
            reasons.Add("Repeated similar donation submissions");
        }

        return reasons;
    }
}