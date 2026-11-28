using CrisisAid.Api.Contracts;
using CrisisAid.Api.Data;
using CrisisAid.Api.Domain;
using CrisisAid.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace CrisisAid.Api.Endpoints;

public static class DonationEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/campaigns", async (AidDbContext db) => await db.Campaigns.ToListAsync());
api.MapPost("/campaigns", async (CampaignDto dto, HttpContext ctx, AidDbContext db) => {
    if (dto.TargetAmount <= 0 || !await db.Crises.AnyAsync(c => c.Id == dto.CrisisId)) return Results.BadRequest();
    var campaign = new DonationCampaign { Name = dto.Name, CrisisId = dto.CrisisId, TargetAmount = dto.TargetAmount };
    db.Campaigns.Add(campaign); TraceLog.Add(db, "Campaign", campaign.Id, "Created", Access.UserId(ctx.User));
    await db.SaveChangesAsync(); return Results.Ok(campaign);
}).RequireAuthorization(p => p.RequireRole(Roles.Funding, Roles.Coordinator, Roles.Admin));
api.MapPost("/donations", async (DonationDto dto, HttpContext ctx, AidDbContext db) => {
    if (dto.Amount <= 0 || !new[] { "LKR", "USD", "EUR" }.Contains(dto.Currency) ||
        !await db.Campaigns.AnyAsync(x => x.Id == dto.CampaignId && x.Status == "Open")) return Results.BadRequest();
    var donation = new Donation { DonorId = Access.UserId(ctx.User), CampaignId = dto.CampaignId,
        Amount = dto.Amount, Currency = dto.Currency, Kind = dto.Kind, Status = "PledgeRecorded" };
    db.Donations.Add(donation); TraceLog.Add(db, "Donation", donation.Id, "PledgeRecorded", donation.DonorId,
        new { donation.CampaignId, donation.Amount, donation.Currency });
    await db.SaveChangesAsync(); return Results.Ok(donation);
});
api.MapGet("/donations", async (HttpContext ctx, AidDbContext db) => {
    IQueryable<Donation> q = db.Donations.AsNoTracking();
    if (!Access.Staff(ctx.User)) q = q.Where(x => x.DonorId == Access.UserId(ctx.User));
    return await q.OrderByDescending(x => x.At).Take(200).ToListAsync();
});
    }
}