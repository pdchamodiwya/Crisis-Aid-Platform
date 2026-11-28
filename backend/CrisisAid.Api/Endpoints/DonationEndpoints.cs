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
        api.MapPost("/campaigns", async (
    CampaignInput dto,
    HttpContext ctx,
    AidDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(dto.Name) ||
        dto.Name.Length > 120 ||
        dto.TargetAmount <= 0 ||
        decimal.Round(dto.TargetAmount, 2) != dto.TargetAmount ||
        !new[] { "LKR", "USD", "EUR" }.Contains(dto.Currency) ||
        (dto.EndDate.HasValue && dto.EndDate < dto.StartDate))
    {
        return Results.BadRequest(new
        {
            error = "Invalid campaign details."
        });
    }

    if (!await db.Crises.AnyAsync(
        x => x.Id == dto.CrisisId))
    {
        return Results.BadRequest(new
        {
            error = "Crisis event does not exist."
        });
    }

    var actor = Access.UserId(ctx.User);
    var account = await db.Users.FindAsync(actor);

    var campaign = new DonationCampaign
    {
        Name = dto.Name.Trim(),
        Description = dto.Description ?? "",
        CrisisId = dto.CrisisId,
        OrganisationId = account?.OrganisationId,
        TargetAmount = dto.TargetAmount,
        Currency = dto.Currency,
        StartDate = dto.StartDate,
        EndDate = dto.EndDate,
        CreatedBy = actor
    };

    db.Campaigns.Add(campaign);

    TraceLog.Add(
        db, "Campaign", campaign.Id,
        "Created", actor);

    await db.SaveChangesAsync();

    return Results.Created(
        $"/api/campaigns/{campaign.Id}",
        campaign);
})
.RequireAuthorization(p =>
    p.RequireRole(Roles.Funding, Roles.Admin));
        api.MapGet("/campaigns", async (AidDbContext db) => await db.Campaigns.ToListAsync());
api.MapPost("/campaigns", async (CampaignDto dto, HttpContext ctx, AidDbContext db) => {
    if (dto.TargetAmount <= 0 || !await db.Crises.AnyAsync(c => c.Id == dto.CrisisId)) return Results.BadRequest();
    var campaign = new DonationCampaign { Name = dto.Name, CrisisId = dto.CrisisId, TargetAmount = dto.TargetAmount };
    db.Campaigns.Add(campaign); TraceLog.Add(db, "Campaign", campaign.Id, "Created", Access.UserId(ctx.User));
    await db.SaveChangesAsync(); return Results.Ok(campaign);
}).RequireAuthorization(p => p.RequireRole(Roles.Funding, Roles.Coordinator, Roles.Admin));


api.MapPost("/donations", async (
    MonetaryDonationInput dto,
    HttpContext ctx,
    AidDbContext db) =>
{
    if (dto.Amount <= 0 ||
        dto.Amount > 9999999999999999.99m ||
        decimal.Round(dto.Amount, 2) != dto.Amount)
    {
        return Results.BadRequest(new
        {
            error = "Enter a valid donation amount."
        });
    }

    var campaign = await db.Campaigns
        .FindAsync(dto.CampaignId);

    if (campaign == null)
        return Results.NotFound();

    var now = DateTime.UtcNow;

    if (campaign.Status != "Open" ||
        campaign.StartDate > now ||
        campaign.EndDate < now)
    {
        return Results.Conflict(new
        {
            error = "Campaign is not accepting donations."
        });
    }

    if (campaign.Currency != dto.Currency)
    {
        return Results.BadRequest(new
        {
            error = "Donation currency must match the campaign."
        });
    }

    var donation = new Donation
    {
        CampaignId = campaign.Id,
        DonorId = Access.UserId(ctx.User),
        Amount = dto.Amount,
        Currency = dto.Currency,
        Kind = "Monetary",
        Status = "PledgeRecorded",
        Notes = dto.Notes
    };

    db.Donations.Add(donation);

    TraceLog.Add(
        db, "Donation", donation.Id,
        "PledgeRecorded", donation.DonorId);

    await db.SaveChangesAsync();

    return Results.Created(
        $"/api/donations/{donation.Id}",
        donation);
})
.RequireAuthorization(p =>
    p.RequireRole(Roles.Donor, Roles.Citizen));
api.MapGet("/donations", async (HttpContext ctx, AidDbContext db) => {
    IQueryable<Donation> q = db.Donations.AsNoTracking();
    if (!Access.Staff(ctx.User)) q = q.Where(x => x.DonorId == Access.UserId(ctx.User));
    return await q.OrderByDescending(x => x.At).Take(200).ToListAsync();
});

    }
}