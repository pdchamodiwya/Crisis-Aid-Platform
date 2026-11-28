
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
        // ==========================================
        // 01 - GET ALL CAMPAIGNS
        // ==========================================

        api.MapGet("/campaigns", async (
            AidDbContext db) =>
        {
            return await db.Campaigns
                .AsNoTracking()
                .ToListAsync();
        });

        // ==========================================
        // 02 - CREATE CAMPAIGN
        // ==========================================

        api.MapPost("/campaigns", async (
            CampaignInput dto,
            HttpContext ctx,
            AidDbContext db) =>
        {
            // Compatibility with the original
            // campaign creation interface.

            var currency = dto.Currency ?? "LKR";

            var startDate = dto.StartDate == default
                ? DateTime.UtcNow
                : dto.StartDate;

            // Validate campaign details.

            if (string.IsNullOrWhiteSpace(dto.Name) ||
                dto.Name.Length > 120 ||
                dto.TargetAmount <= 0 ||
                dto.TargetAmount > 9999999999999999.99m ||
                decimal.Round(dto.TargetAmount, 2)
                    != dto.TargetAmount ||
                !new[] { "LKR", "USD", "EUR" }
                    .Contains(currency) ||
                (dto.EndDate.HasValue &&
                 dto.EndDate.Value < startDate))
            {
                return Results.BadRequest(new
                {
                    error = "Invalid campaign details."
                });
            }

            // Check whether the crisis exists.

            if (!await db.Crises.AnyAsync(
                x => x.Id == dto.CrisisId))
            {
                return Results.BadRequest(new
                {
                    error = "Crisis event does not exist."
                });
            }

            // Retrieve the authenticated account.

            var actor = Access.UserId(ctx.User);

            var account = await db.Users.FindAsync(actor);

            if (account == null || !account.IsActive)
            {
                return Results.Unauthorized();
            }

            // Create the campaign.

            var campaign = new DonationCampaign
            {
                Name = dto.Name.Trim(),
                Description = dto.Description ?? "",
                CrisisId = dto.CrisisId,
                OrganisationId = account.OrganisationId,
                TargetAmount = dto.TargetAmount,
                Currency = currency,
                StartDate = startDate,
                EndDate = dto.EndDate,
                Status = "Open",
                CreatedBy = actor
            };

            db.Campaigns.Add(campaign);

            TraceLog.Add(
                db,
                "Campaign",
                campaign.Id,
                "Created",
                actor);

            await db.SaveChangesAsync();

            return Results.Created(
                $"/api/campaigns/{campaign.Id}",
                campaign);
        })
        .RequireAuthorization(p =>
            p.RequireRole(
                Roles.Funding,
                Roles.Coordinator,
                Roles.Admin));

        // ==========================================
        // 03 - REGISTER MONETARY DONATION
        // ==========================================

        api.MapPost("/donations", async (
            MonetaryDonationInput dto,
            HttpContext ctx,
            AidDbContext db) =>
        {
            // Validate donation amount.

            if (dto.Amount <= 0 ||
                dto.Amount > 9999999999999999.99m ||
                decimal.Round(dto.Amount, 2)
                    != dto.Amount)
            {
                return Results.BadRequest(new
                {
                    error = "Enter a valid donation amount."
                });
            }

            // Find the selected campaign.

            var campaign = await db.Campaigns
                .FindAsync(dto.CampaignId);

            if (campaign == null)
            {
                return Results.NotFound(new
                {
                    error = "Campaign not found."
                });
            }

            // Check campaign availability.

            var now = DateTime.UtcNow;

            if (campaign.Status != "Open" ||
                campaign.StartDate > now ||
                (campaign.EndDate.HasValue &&
                 campaign.EndDate.Value < now))
            {
                return Results.Conflict(new
                {
                    error =
                        "Campaign is not accepting donations."
                });
            }

            // Validate donation currency.

            if (campaign.Currency != dto.Currency)
            {
                return Results.BadRequest(new
                {
                    error =
                        "Donation currency must match the campaign."
                });
            }

            // Create the monetary donation pledge.

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
                db,
                "Donation",
                donation.Id,
                "PledgeRecorded",
                donation.DonorId);

            await db.SaveChangesAsync();

            return Results.Created(
                $"/api/donations/{donation.Id}",
                donation);
        })
        .RequireAuthorization(p =>
            p.RequireRole(
                Roles.Donor,
                Roles.Citizen));

        // ==========================================
        // 04 - GET DONATIONS
        // ==========================================

        api.MapGet("/donations", async (
            HttpContext ctx,
            AidDbContext db) =>
        {
            IQueryable<Donation> q =
                db.Donations.AsNoTracking();

            if (!Access.CanSeeAllDonations(ctx.User))
            {
                q = q.Where(x =>
                    x.DonorId ==
                    Access.UserId(ctx.User));
            }

            return await q
                .OrderByDescending(x => x.At)
                .Take(200)
                .ToListAsync();
        });

        // ==========================================
        // 05 - REGISTER MATERIAL DONATION
        // ==========================================

        api.MapPost("/donations/material", async (
            MaterialDonationInput dto,
            HttpContext ctx,
            AidDbContext db) =>
        {
            // Validate quantity.

            if (dto.Quantity <= 0)
            {
                return Results.BadRequest(new
                {
                    error =
                        "Quantity must be greater than zero."
                });
            }

            // Validate required fields.

            if (string.IsNullOrWhiteSpace(dto.Category) ||
                string.IsNullOrWhiteSpace(dto.ItemName) ||
                string.IsNullOrWhiteSpace(dto.Unit) ||
                string.IsNullOrWhiteSpace(dto.Condition) ||
                string.IsNullOrWhiteSpace(
                    dto.ReceivingLocation))
            {
                return Results.BadRequest(new
                {
                    error =
                        "All material donation fields are required."
                });
            }

            // Validate material category.

            var allowedCategories = new[]
            {
                "Food",
                "Water",
                "Medicine",
                "Clothing",
                "Shelter",
                "Other"
            };

            if (!allowedCategories.Contains(dto.Category))
            {
                return Results.BadRequest(new
                {
                    error =
                        "Invalid material donation category."
                });
            }

            // Validate item condition.

            var allowedConditions = new[]
            {
                "New",
                "Good",
                "RequiresInspection"
            };

            if (!allowedConditions.Contains(dto.Condition))
            {
                return Results.BadRequest(new
                {
                    error = "Invalid item condition."
                });
            }

            // Validate field lengths.

            if (dto.ItemName.Length > 150 ||
                dto.Unit.Length > 30 ||
                dto.ReceivingLocation.Length > 200)
            {
                return Results.BadRequest(new
                {
                    error =
                        "One or more fields exceed the allowed length."
                });
            }

            // Find the campaign.

            var campaign = await db.Campaigns
                .FindAsync(dto.CampaignId);

            if (campaign == null)
            {
                return Results.NotFound(new
                {
                    error = "Campaign not found."
                });
            }

            // Check whether the campaign accepts donations.

            var now = DateTime.UtcNow;

            if (campaign.Status != "Open" ||
                campaign.StartDate > now ||
                (campaign.EndDate.HasValue &&
                 campaign.EndDate.Value < now))
            {
                return Results.Conflict(new
                {
                    error =
                        "Campaign is not accepting donations."
                });
            }

            // Create the main donation record.

            var donation = new Donation
            {
                CampaignId = campaign.Id,
                DonorId = Access.UserId(ctx.User),
                Kind = "Material",
                Currency = campaign.Currency,
                Amount = 0,
                Status = "PledgeRecorded"
            };

            // Create the material item.

            var item = new MaterialDonationItem
            {
                DonationId = donation.Id,
                Category = dto.Category,
                ItemName = dto.ItemName.Trim(),
                Quantity = dto.Quantity,
                Unit = dto.Unit.Trim(),
                Condition = dto.Condition,
                ReceivingLocation =
                    dto.ReceivingLocation.Trim()
            };

            // Add both records to the database.

            db.Donations.Add(donation);

            db.MaterialDonationItems.Add(item);

            // Record audit information.

            TraceLog.Add(
                db,
                "Donation",
                donation.Id,
                "MaterialPledgeRecorded",
                donation.DonorId,
                new
                {
                    item.Category,
                    item.ItemName,
                    item.Quantity,
                    item.Unit
                });

            // Save all records together.

            await db.SaveChangesAsync();

            return Results.Created(
                $"/api/donations/{donation.Id}/materials",
                new
                {
                    message =
                        "Material donation pledge recorded successfully.",
                    donation,
                    materialItem = item
                });
        })
        .RequireAuthorization(p =>
            p.RequireRole(
                Roles.Donor,
                Roles.Citizen));

        // ==========================================
        // 06 - GET MATERIAL DONATION DETAILS
        // ==========================================

        api.MapGet("/donations/{id:guid}/materials", async (
            Guid id,
            HttpContext ctx,
            AidDbContext db) =>
        {
            // Find the main donation.

            var donation = await db.Donations
                .FindAsync(id);

            if (donation == null)
            {
                return Results.NotFound(new
                {
                    error = "Donation not found."
                });
            }

            var userId = Access.UserId(ctx.User);

            var role = Access.Role(ctx.User);

            // Validate ownership and authorisation.

            if (donation.DonorId != userId &&
                role != Roles.Admin)
            {
                if (role != Roles.Funding)
                {
                    return Results.Forbid();
                }

                var account = await db.Users
                    .FindAsync(userId);

                var campaign = await db.Campaigns
                    .FindAsync(donation.CampaignId);

                if (account == null ||
                    campaign == null ||
                    account.OrganisationId == null ||
                    account.OrganisationId
                        != campaign.OrganisationId)
                {
                    return Results.Forbid();
                }
            }

            // Retrieve material item details.

            var items = await db.MaterialDonationItems
                .AsNoTracking()
                .Where(x => x.DonationId == id)
                .ToListAsync();

            return Results.Ok(items);
        });
    }
}
