
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

        // ==========================================
        // 07 - CONFIRM DONATION
        // ==========================================

        api.MapPost("/donations/{id:guid}/confirm", async (
            Guid id,
            ConfirmDonationInput dto,
            HttpContext ctx,
            AidDbContext db) =>
        {
            try
            {
                // Start a transaction to protect
                // financial records from concurrent updates.

                await using var tx =
                    await db.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                // 1. Check the authenticated officer.

                var actorId = Access.UserId(ctx.User);
                var role = Access.Role(ctx.User);

                var account = await db.Users
                    .FindAsync(actorId);

                if (account == null ||
                    !account.IsActive ||
                    account.Role != role)
                {
                    return Results.Unauthorized();
                }

                // 2. Find the donation.

                var donation = await db.Donations
                    .FindAsync(id);

                if (donation == null)
                {
                    return Results.NotFound(new
                    {
                        error = "Donation not found."
                    });
                }

                // 3. Find the associated campaign.

                var campaign = await db.Campaigns
                    .FindAsync(donation.CampaignId);

                if (campaign == null)
                {
                    return Results.NotFound(new
                    {
                        error = "Campaign not found."
                    });
                }

                // 4. Check organisation permissions.

                if (role != Roles.Admin &&
                    (account.OrganisationId == null ||
                     account.OrganisationId !=
                         campaign.OrganisationId))
                {
                    return Results.Forbid();
                }

                // 5. Prevent repeated confirmation.

                if (donation.Status == "Confirmed")
                {
                    return Results.Conflict(new
                    {
                        error =
                            "Donation has already been confirmed."
                    });
                }

                // 6. Validate the current donation status.

                if (donation.Status != "PledgeRecorded" &&
                    donation.Status != "UnderReview")
                {
                    return Results.Conflict(new
                    {
                        error =
                            "This donation cannot be confirmed.",
                        currentStatus = donation.Status
                    });
                }

                // 7. Require actual receipt verification.

                if (!dto.ReceiptVerified)
                {
                    return Results.BadRequest(new
                    {
                        error =
                            "Verify that the payment or goods have been received before confirming."
                    });
                }

                if (string.IsNullOrWhiteSpace(dto.Notes) ||
                    dto.Notes.Length > 1000)
                {
                    return Results.BadRequest(new
                    {
                        error =
                            "Valid confirmation notes are required."
                    });
                }

                // 8. Check for unresolved investigations.

                var unresolvedFlags =
                    await db.DonationFraudFlags.AnyAsync(x =>
                        x.DonationId == id &&
                        (x.Status == "Open" ||
                         x.Status == "Investigating"));

                if (unresolvedFlags)
                {
                    return Results.Conflict(new
                    {
                        error =
                            "Resolve outstanding donation investigations before confirmation."
                    });
                }

                MaterialDonationItem? materialItem = null;

                // ======================================
                // 9. MONETARY DONATION CONFIRMATION
                // ======================================

                if (donation.Kind == "Monetary")
                {
                    if (donation.Amount <= 0)
                    {
                        return Results.BadRequest(new
                        {
                            error =
                                "Invalid monetary donation amount."
                        });
                    }

                    var reference =
                        dto.TransactionReference?
                            .Trim()
                            .ToUpperInvariant();

                    if (string.IsNullOrWhiteSpace(reference) ||
                        reference.Length > 150 ||
                        string.IsNullOrWhiteSpace(
                            dto.PaymentMethod))
                    {
                        return Results.BadRequest(new
                        {
                            error =
                                "A valid transaction reference and payment method are required."
                        });
                    }

                    var allowedPaymentMethods = new[]
                    {
                        "BankTransfer",
                        "Cash",
                        "Card",
                        "Other"
                    };

                    if (!allowedPaymentMethods.Contains(
                        dto.PaymentMethod))
                    {
                        return Results.BadRequest(new
                        {
                            error = "Invalid payment method."
                        });
                    }

                    // Detect a transaction reference
                    // already used for another confirmed
                    // monetary donation.

                    bool duplicateReference =
                        await db.Donations.AnyAsync(x =>
                            x.Id != donation.Id &&
                            x.Kind == "Monetary" &&
                            x.Status == "Confirmed" &&
                            x.TransactionReference != null &&
                            x.TransactionReference.ToUpper()
                                == reference);

                    if (duplicateReference)
                    {
                        donation.Status = "UnderReview";

                        donation.TransactionReference =
                            reference;

                        donation.PaymentMethod =
                            dto.PaymentMethod;

                        var flag = new DonationFraudFlag
                        {
                            DonationId = donation.Id,
                            Reason =
                                "Possible duplicate transaction reference",
                            Severity = "High",
                            Status = "Open"
                        };

                        db.DonationFraudFlags.Add(flag);

                        TraceLog.Add(
                            db,
                            "Donation",
                            donation.Id,
                            "DuplicateReferenceFlagged",
                            actorId);

                        await db.SaveChangesAsync();
                        await tx.CommitAsync();

                        return Results.Conflict(new
                        {
                            error =
                                "Transaction reference has already been used. Donation flagged for investigation.",
                            status = donation.Status
                        });
                    }

                    // Save the verified payment information.

                    donation.TransactionReference =
                        reference;

                    donation.PaymentMethod =
                        dto.PaymentMethod;
                }

                // ======================================
                // 10. MATERIAL DONATION CONFIRMATION
                // ======================================

                else if (donation.Kind == "Material")
                {
                    var items =
                        await db.MaterialDonationItems
                            .Where(x =>
                                x.DonationId == donation.Id)
                            .ToListAsync();

                    // The current prototype supports
                    // one material item per donation.

                    if (items.Count != 1)
                    {
                        return Results.Conflict(new
                        {
                            error =
                                "Expected one material item for this donation."
                        });
                    }

                    materialItem = items[0];

                    if (!dto.ReceivedQuantity.HasValue ||
                        dto.ReceivedQuantity.Value <= 0 ||
                        dto.ReceivedQuantity.Value >
                            materialItem.Quantity)
                    {
                        return Results.BadRequest(new
                        {
                            error =
                                "Received quantity must be positive and cannot exceed the pledged quantity.",
                            pledgedQuantity =
                                materialItem.Quantity
                        });
                    }

                    // Record the actual received quantity.

                    materialItem.ReceivedQuantity =
                        dto.ReceivedQuantity.Value;
                }

                else
                {
                    return Results.BadRequest(new
                    {
                        error = "Unsupported donation type."
                    });
                }

                // ======================================
                // 11. CONFIRM THE DONATION
                // ======================================

                donation.Status = "Confirmed";

                donation.ConfirmedBy = actorId;

                donation.ReceivedAt = DateTime.UtcNow;

                // ======================================
                // 12. RECORD AUDIT INFORMATION
                // ======================================

                TraceLog.Add(
                    db,
                    "Donation",
                    donation.Id,
                    "Confirmed",
                    actorId,
                    new
                    {
                        donation.Kind,
                        donation.Amount,
                        donation.Currency,
                        donation.PaymentMethod,
                        receivedQuantity =
                            materialItem?.ReceivedQuantity,
                        confirmationNotes =
                            dto.Notes.Trim()
                    });

                // ======================================
                // 13. QUEUE DONOR NOTIFICATION
                // ======================================

                TraceLog.Queue(
                    db,
                    donation.DonorId,
                    $"donation:{donation.Id}:confirmed",
                    "Donation confirmed",
                    "Your donation has been received and confirmed. Thank you for supporting humanitarian assistance.");

                // ======================================
                // 14. SAVE ALL CHANGES
                // ======================================

                await db.SaveChangesAsync();

                await tx.CommitAsync();

                return Results.Ok(new
                {
                    message =
                        "Donation confirmed successfully.",
                    donation,
                    materialItem
                });
            }
            catch (Exception ex) when (
                ex is Microsoft.Data.SqlClient.SqlException
                {
                    Number: 1205 or 2601 or 2627
                } ||
                ex is DbUpdateException
                {
                    InnerException:
                        Microsoft.Data.SqlClient.SqlException
                        {
                            Number: 1205 or 2601 or 2627
                        }
                })
            {
                return Results.Conflict(new
                {
                    error =
                        "A concurrent update occurred. Reload the donation and try again."
                });
            }
        })
        .RequireAuthorization(p =>
            p.RequireRole(
                Roles.Funding,
                Roles.Admin));


        // ==========================================
        // 08 - REJECT DONATION
        // ==========================================

        api.MapPost("/donations/{id:guid}/reject", async (
            Guid id,
            RejectDonationInput dto,
            HttpContext ctx,
            AidDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Reason) ||
                dto.Reason.Length > 1000)
            {
                return Results.BadRequest(new
                {
                    error =
                        "A valid rejection reason is required."
                });
            }

            try
            {
                await using var tx =
                    await db.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var actorId = Access.UserId(ctx.User);
                var role = Access.Role(ctx.User);

                var account = await db.Users
                    .FindAsync(actorId);

                if (account == null ||
                    !account.IsActive ||
                    account.Role != role)
                {
                    return Results.Unauthorized();
                }

                var donation = await db.Donations
                    .FindAsync(id);

                if (donation == null)
                {
                    return Results.NotFound(new
                    {
                        error = "Donation not found."
                    });
                }

                var campaign = await db.Campaigns
                    .FindAsync(donation.CampaignId);

                if (campaign == null)
                {
                    return Results.NotFound(new
                    {
                        error = "Campaign not found."
                    });
                }

                // Validate organisation ownership.

                if (role != Roles.Admin &&
                    (account.OrganisationId == null ||
                     account.OrganisationId !=
                         campaign.OrganisationId))
                {
                    return Results.Forbid();
                }

                // Only unconfirmed donations
                // may be rejected.

                if (donation.Status != "PledgeRecorded" &&
                    donation.Status != "UnderReview")
                {
                    return Results.Conflict(new
                    {
                        error =
                            "This donation cannot be rejected.",
                        currentStatus = donation.Status
                    });
                }

                // Update donation status.

                donation.Status = "Rejected";

                // Record the rejection reason.

                TraceLog.Add(
                    db,
                    "Donation",
                    donation.Id,
                    "Rejected",
                    actorId,
                    new
                    {
                        reason = dto.Reason.Trim()
                    });

                // Notify the donor.

                TraceLog.Queue(
                    db,
                    donation.DonorId,
                    $"donation:{donation.Id}:rejected",
                    "Donation update",
                    "Your donation could not be accepted. Please contact the funding officer for further information.");

                await db.SaveChangesAsync();

                await tx.CommitAsync();

                return Results.Ok(new
                {
                    message =
                        "Donation rejected successfully.",
                    donationId = donation.Id,
                    status = donation.Status
                });
            }
            catch (Exception ex) when (
                ex is Microsoft.Data.SqlClient.SqlException
                {
                    Number: 1205 or 2601 or 2627
                } ||
                ex is DbUpdateException
                {
                    InnerException:
                        Microsoft.Data.SqlClient.SqlException
                        {
                            Number: 1205 or 2601 or 2627
                        }
                })
            {
                return Results.Conflict(new
                {
                    error =
                        "A concurrent update occurred. Reload the donation and try again."
                });
            }
        })
        .RequireAuthorization(p =>
            p.RequireRole(
                Roles.Funding,
                Roles.Admin));

                api.MapPost("/donation-allocations", async (
    DonationAllocationInput dto,
    HttpContext ctx,
    AidDbContext db,
    DonationService service) =>
{
    if (dto.Amount <= 0 ||
        decimal.Round(dto.Amount, 2) != dto.Amount ||
        string.IsNullOrWhiteSpace(dto.Purpose) ||
        !new[] {
            "Food", "Water", "Medicine",
            "Transport", "Shelter", "Other"
        }.Contains(dto.Category))
    {
        return Results.BadRequest(new
        {
            error = "Invalid allocation details."
        });
    }

    await using var transaction =
        await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);

    var donation = await db.Donations
        .FindAsync(dto.DonationId);

    if (donation == null)
        return Results.NotFound();

    if (donation.Status != "Confirmed" ||
        donation.Kind != "Monetary")
    {
        return Results.Conflict(new
        {
            error = "Only confirmed monetary donations can be allocated."
        });
    }

    var campaign = await db.Campaigns
        .FindAsync(donation.CampaignId);

    var actor = await db.Users
        .FindAsync(Access.UserId(ctx.User));

    if (campaign == null || actor == null)
        return Results.NotFound();

    if (Access.Role(ctx.User) != Roles.Admin &&
        (actor.OrganisationId == null ||
         campaign.OrganisationId != actor.OrganisationId))
    {
        return Results.Forbid();
    }

    var available =
        await service.GetAvailableFundsAsync(donation.Id);

    if (dto.Amount > available)
    {
        return Results.Conflict(new
        {
            error = "Insufficient available funds.",
            available
        });
    }

    var allocation = new DonationAllocation
    {
        DonationId = donation.Id,
        Category = dto.Category,
        Purpose = dto.Purpose.Trim(),
        Amount = dto.Amount,
        AllocatedBy = actor.Id,
        Status = "Approved"
    };

    db.DonationAllocations.Add(allocation);

    TraceLog.Add(
        db, "DonationAllocation",
        allocation.Id, "Approved", actor.Id);

    await db.SaveChangesAsync();
    await transaction.CommitAsync();

    return Results.Created(
        $"/api/donation-allocations/{allocation.Id}",
        allocation);
})
.RequireAuthorization(p =>
    p.RequireRole(Roles.Funding, Roles.Admin));

    api.MapGet("/donation-allocations", async (
    HttpContext ctx,
    AidDbContext db) =>
{
    var actor = await db.Users
        .FindAsync(Access.UserId(ctx.User));

    if (actor == null)
        return Results.Forbid();

    var admin = Access.Role(ctx.User) == Roles.Admin;

    var query =
        from a in db.DonationAllocations
        join d in db.Donations
            on a.DonationId equals d.Id
        join c in db.Campaigns
            on d.CampaignId equals c.Id
        where admin ||
            (actor.OrganisationId != null &&
             c.OrganisationId == actor.OrganisationId)
        select new
        {
            a.Id,
            a.DonationId,
            a.Category,
            a.Purpose,
            a.Amount,
            a.Status,
            a.AllocatedAt,
            campaignName = c.Name,
            currency = d.Currency,
            spent = db.CampaignExpenditures
                .Where(e => e.AllocationId == a.Id)
                .Sum(e => (decimal?)e.Amount) ?? 0
        };

    return Results.Ok(await query.ToListAsync());
})
.RequireAuthorization(p =>
    p.RequireRole(Roles.Funding, Roles.Admin));


    }
}
