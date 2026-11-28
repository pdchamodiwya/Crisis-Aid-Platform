using CrisisAid.Api.Data;
using CrisisAid.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Routing;

namespace CrisisAid.Api.Services;

public record AlertInput(Guid CrisisId, string Headline, string Message, string Severity, string Language);
public record HouseholdInput(string Area, int Members, bool AccessibilityNeeded, string Contact);
public record FeedbackInput(Guid RequestId, int Rating, string Comments);
public record PartnerOfferInput(Guid OrganisationId, string ResourceCategory, string Area, int Quantity, string Notes);

public static class ExtendedModules
{
    // Idempotent additive schema patch for existing EnsureCreated() databases.
    // Existing tables and rows are preserved; no destructive reset is performed.
    public static async Task EnsureSchemaAsync(AidDbContext db)
    {
        const string sql = """
        IF OBJECT_ID(N'dbo.PublicAlerts', N'U') IS NULL
        CREATE TABLE dbo.PublicAlerts (Id uniqueidentifier NOT NULL PRIMARY KEY, CrisisId uniqueidentifier NOT NULL, Headline nvarchar(max) NOT NULL, Message nvarchar(max) NOT NULL, Severity nvarchar(max) NOT NULL, Language nvarchar(max) NOT NULL, Status nvarchar(max) NOT NULL, CreatedAt datetime2 NOT NULL, PublishedBy uniqueidentifier NOT NULL);
        IF OBJECT_ID(N'dbo.Households', N'U') IS NULL
        CREATE TABLE dbo.Households (Id uniqueidentifier NOT NULL PRIMARY KEY, UserId uniqueidentifier NOT NULL UNIQUE, Area nvarchar(max) NOT NULL, Members int NOT NULL, AccessibilityNeeded bit NOT NULL, Contact nvarchar(max) NOT NULL, UpdatedAt datetime2 NOT NULL);
        IF OBJECT_ID(N'dbo.Feedback', N'U') IS NULL
        CREATE TABLE dbo.Feedback (Id uniqueidentifier NOT NULL PRIMARY KEY, RequestId uniqueidentifier NOT NULL UNIQUE, CitizenId uniqueidentifier NOT NULL, Rating int NOT NULL, Comments nvarchar(max) NOT NULL, CreatedAt datetime2 NOT NULL);
        IF OBJECT_ID(N'dbo.PartnerOffers', N'U') IS NULL
        CREATE TABLE dbo.PartnerOffers (Id uniqueidentifier NOT NULL PRIMARY KEY, OrganisationId uniqueidentifier NOT NULL, CreatedBy uniqueidentifier NOT NULL, ResourceCategory nvarchar(max) NOT NULL, Area nvarchar(max) NOT NULL, Quantity int NOT NULL, Notes nvarchar(max) NOT NULL, Status nvarchar(max) NOT NULL, CreatedAt datetime2 NOT NULL);
        IF COL_LENGTH(N'dbo.Shelters', N'ContactName') IS NULL ALTER TABLE dbo.Shelters ADD ContactName nvarchar(max) NOT NULL CONSTRAINT DF_Shelters_ContactName DEFAULT N'';
        IF COL_LENGTH(N'dbo.Shelters', N'ContactPhone') IS NULL ALTER TABLE dbo.Shelters ADD ContactPhone nvarchar(max) NOT NULL CONSTRAINT DF_Shelters_ContactPhone DEFAULT N'';
        IF COL_LENGTH(N'dbo.Shelters', N'ContactEmail') IS NULL ALTER TABLE dbo.Shelters ADD ContactEmail nvarchar(max) NOT NULL CONSTRAINT DF_Shelters_ContactEmail DEFAULT N'';
        IF COL_LENGTH(N'dbo.Shelters', N'OpeningHours') IS NULL ALTER TABLE dbo.Shelters ADD OpeningHours nvarchar(max) NOT NULL CONSTRAINT DF_Shelters_OpeningHours DEFAULT N'';
        IF COL_LENGTH(N'dbo.Shelters', N'MedicalFacilities') IS NULL ALTER TABLE dbo.Shelters ADD MedicalFacilities bit NOT NULL CONSTRAINT DF_Shelters_MedicalFacilities DEFAULT 0;
        IF COL_LENGTH(N'dbo.Shelters', N'SanitationFacilities') IS NULL ALTER TABLE dbo.Shelters ADD SanitationFacilities bit NOT NULL CONSTRAINT DF_Shelters_SanitationFacilities DEFAULT 0;
        IF COL_LENGTH(N'dbo.Shelters', N'FoodAvailable') IS NULL ALTER TABLE dbo.Shelters ADD FoodAvailable bit NOT NULL CONSTRAINT DF_Shelters_FoodAvailable DEFAULT 0;
        IF COL_LENGTH(N'dbo.Shelters', N'AdministratorId') IS NULL ALTER TABLE dbo.Shelters ADD AdministratorId uniqueidentifier NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Shelters_Users_AdministratorId') ALTER TABLE dbo.Shelters ADD CONSTRAINT FK_Shelters_Users_AdministratorId FOREIGN KEY (AdministratorId) REFERENCES dbo.Users(Id);
        IF COL_LENGTH(N'dbo.ShelterAllocations', N'Status') IS NULL ALTER TABLE dbo.ShelterAllocations ADD Status nvarchar(max) NOT NULL CONSTRAINT DF_ShelterAllocations_Status DEFAULT N'CheckedIn';
        IF COL_LENGTH(N'dbo.Volunteers', N'Latitude') IS NULL ALTER TABLE dbo.Volunteers ADD Latitude float NULL;
        IF COL_LENGTH(N'dbo.Volunteers', N'Longitude') IS NULL ALTER TABLE dbo.Volunteers ADD Longitude float NULL;
        IF COL_LENGTH(N'dbo.VolunteerTasks', N'ScheduledStartAt') IS NULL ALTER TABLE dbo.VolunteerTasks ADD ScheduledStartAt datetime2 NULL;
        IF COL_LENGTH(N'dbo.VolunteerTasks', N'DurationMinutes') IS NULL ALTER TABLE dbo.VolunteerTasks ADD DurationMinutes int NULL;
        IF COL_LENGTH(N'dbo.VolunteerTasks', N'Urgency') IS NULL ALTER TABLE dbo.VolunteerTasks ADD Urgency nvarchar(max) NOT NULL CONSTRAINT DF_VolunteerTasks_Urgency DEFAULT N'Medium';
        IF COL_LENGTH(N'dbo.VolunteerTasks', N'Latitude') IS NULL ALTER TABLE dbo.VolunteerTasks ADD Latitude float NULL;
        IF COL_LENGTH(N'dbo.VolunteerTasks', N'Longitude') IS NULL ALTER TABLE dbo.VolunteerTasks ADD Longitude float NULL;
        IF COL_LENGTH(N'dbo.VolunteerTasks', N'DeclineReason') IS NULL ALTER TABLE dbo.VolunteerTasks ADD DeclineReason nvarchar(max) NOT NULL CONSTRAINT DF_VolunteerTasks_DeclineReason DEFAULT N'';
        IF COL_LENGTH(N'dbo.VolunteerTasks', N'HoursWorked') IS NULL ALTER TABLE dbo.VolunteerTasks ADD HoursWorked decimal(9,2) NULL;
        IF COL_LENGTH(N'dbo.VolunteerTasks', N'CompletionNotes') IS NULL ALTER TABLE dbo.VolunteerTasks ADD CompletionNotes nvarchar(max) NOT NULL CONSTRAINT DF_VolunteerTasks_CompletionNotes DEFAULT N'';
        IF COL_LENGTH(N'dbo.VolunteerTasks', N'CoordinatorVerified') IS NULL ALTER TABLE dbo.VolunteerTasks ADD CoordinatorVerified bit NOT NULL CONSTRAINT DF_VolunteerTasks_CoordinatorVerified DEFAULT 0;
        IF COL_LENGTH(N'dbo.VolunteerTasks', N'VerifiedBy') IS NULL ALTER TABLE dbo.VolunteerTasks ADD VerifiedBy uniqueidentifier NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_VolunteerTasks_Users_VerifiedBy') ALTER TABLE dbo.VolunteerTasks ADD CONSTRAINT FK_VolunteerTasks_Users_VerifiedBy FOREIGN KEY (VerifiedBy) REFERENCES dbo.Users(Id);
        IF COL_LENGTH(N'dbo.VolunteerTasks', N'VerifiedAt') IS NULL ALTER TABLE dbo.VolunteerTasks ADD VerifiedAt datetime2 NULL;
        IF COL_LENGTH(N'dbo.VolunteerTasks', N'CheckInIntervalMinutes') IS NULL ALTER TABLE dbo.VolunteerTasks ADD CheckInIntervalMinutes int NOT NULL CONSTRAINT DF_VolunteerTasks_CheckInIntervalMinutes DEFAULT 30;
        IF COL_LENGTH(N'dbo.VolunteerTasks', N'NextCheckInDueAt') IS NULL ALTER TABLE dbo.VolunteerTasks ADD NextCheckInDueAt datetime2 NULL;
        IF COL_LENGTH(N'dbo.VolunteerTasks', N'OverdueAlertSentAt') IS NULL ALTER TABLE dbo.VolunteerTasks ADD OverdueAlertSentAt datetime2 NULL;
        IF OBJECT_ID(N'dbo.ShelterStays', N'U') IS NULL
        CREATE TABLE dbo.ShelterStays (Id uniqueidentifier NOT NULL PRIMARY KEY, AllocationId uniqueidentifier NOT NULL UNIQUE, HouseholdId uniqueidentifier NOT NULL, CheckInAt datetime2 NULL, CheckOutAt datetime2 NULL, OccupancyCount int NOT NULL, Status nvarchar(max) NOT NULL, CONSTRAINT FK_ShelterStays_ShelterAllocations FOREIGN KEY (AllocationId) REFERENCES dbo.ShelterAllocations(Id));
        IF OBJECT_ID(N'dbo.AssistanceCentres', N'U') IS NULL
        CREATE TABLE dbo.AssistanceCentres (Id uniqueidentifier NOT NULL PRIMARY KEY, Name nvarchar(max) NOT NULL, Type nvarchar(max) NOT NULL, Area nvarchar(max) NOT NULL, Facilities nvarchar(max) NOT NULL, Accessible bit NOT NULL, ContactName nvarchar(max) NOT NULL, ContactPhone nvarchar(max) NOT NULL, ContactEmail nvarchar(max) NOT NULL, OpeningHours nvarchar(max) NOT NULL, Status nvarchar(max) NOT NULL, Latitude float NOT NULL, Longitude float NOT NULL, AdministratorId uniqueidentifier NULL, CONSTRAINT FK_AssistanceCentres_Users FOREIGN KEY (AdministratorId) REFERENCES dbo.Users(Id));
        IF OBJECT_ID(N'dbo.ShelterSupplyRequests', N'U') IS NULL
        CREATE TABLE dbo.ShelterSupplyRequests (Id uniqueidentifier NOT NULL PRIMARY KEY, ShelterId uniqueidentifier NOT NULL, RequestedBy uniqueidentifier NOT NULL, CrisisId uniqueidentifier NOT NULL, Category nvarchar(max) NOT NULL, Description nvarchar(max) NOT NULL, Quantity int NOT NULL, Status nvarchar(max) NOT NULL, AssistanceRequestId uniqueidentifier NULL, CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NOT NULL, CONSTRAINT FK_ShelterSupplyRequests_Shelters FOREIGN KEY (ShelterId) REFERENCES dbo.Shelters(Id), CONSTRAINT FK_ShelterSupplyRequests_Users FOREIGN KEY (RequestedBy) REFERENCES dbo.Users(Id), CONSTRAINT FK_ShelterSupplyRequests_Crises FOREIGN KEY (CrisisId) REFERENCES dbo.Crises(Id), CONSTRAINT FK_ShelterSupplyRequests_Requests FOREIGN KEY (AssistanceRequestId) REFERENCES dbo.Requests(Id));
        IF OBJECT_ID(N'dbo.VolunteerSkills', N'U') IS NULL
        CREATE TABLE dbo.VolunteerSkills (Id uniqueidentifier NOT NULL PRIMARY KEY, VolunteerId uniqueidentifier NOT NULL, Name nvarchar(80) NOT NULL, Verified bit NOT NULL, VerifiedBy uniqueidentifier NULL, VerifiedAt datetime2 NULL, CONSTRAINT FK_VolunteerSkills_Volunteers FOREIGN KEY (VolunteerId) REFERENCES dbo.Volunteers(Id), CONSTRAINT FK_VolunteerSkills_Users FOREIGN KEY (VerifiedBy) REFERENCES dbo.Users(Id));
        IF OBJECT_ID(N'dbo.VolunteerAvailabilities', N'U') IS NULL
        CREATE TABLE dbo.VolunteerAvailabilities (Id uniqueidentifier NOT NULL PRIMARY KEY, VolunteerId uniqueidentifier NOT NULL, StartsAt datetime2 NOT NULL, EndsAt datetime2 NOT NULL, CONSTRAINT FK_VolunteerAvailabilities_Volunteers FOREIGN KEY (VolunteerId) REFERENCES dbo.Volunteers(Id));
        IF OBJECT_ID(N'dbo.VolunteerQualifications', N'U') IS NULL
        CREATE TABLE dbo.VolunteerQualifications (Id uniqueidentifier NOT NULL PRIMARY KEY, VolunteerId uniqueidentifier NOT NULL, Name nvarchar(max) NOT NULL, Issuer nvarchar(max) NOT NULL, Reference nvarchar(max) NOT NULL, Verified bit NOT NULL, VerifiedBy uniqueidentifier NULL, VerifiedAt datetime2 NULL, ExpiresAt datetime2 NULL, CONSTRAINT FK_VolunteerQualifications_Volunteers FOREIGN KEY (VolunteerId) REFERENCES dbo.Volunteers(Id), CONSTRAINT FK_VolunteerQualifications_Users FOREIGN KEY (VerifiedBy) REFERENCES dbo.Users(Id));
        IF OBJECT_ID(N'dbo.VolunteerActivityLogs', N'U') IS NULL
        CREATE TABLE dbo.VolunteerActivityLogs (Id uniqueidentifier NOT NULL PRIMARY KEY, VolunteerId uniqueidentifier NOT NULL, TaskId uniqueidentifier NULL, ActorId uniqueidentifier NULL, Activity nvarchar(max) NOT NULL, Notes nvarchar(max) NOT NULL, HoursWorked decimal(9,2) NULL, At datetime2 NOT NULL, CONSTRAINT FK_VolunteerActivityLogs_Volunteers FOREIGN KEY (VolunteerId) REFERENCES dbo.Volunteers(Id), CONSTRAINT FK_VolunteerActivityLogs_Tasks FOREIGN KEY (TaskId) REFERENCES dbo.VolunteerTasks(Id), CONSTRAINT FK_VolunteerActivityLogs_Users FOREIGN KEY (ActorId) REFERENCES dbo.Users(Id));
        IF OBJECT_ID(N'dbo.VolunteerCheckIns', N'U') IS NULL
        CREATE TABLE dbo.VolunteerCheckIns (Id uniqueidentifier NOT NULL PRIMARY KEY, TaskId uniqueidentifier NOT NULL, VolunteerId uniqueidentifier NOT NULL, At datetime2 NOT NULL, Latitude float NULL, Longitude float NULL, Area nvarchar(max) NOT NULL, Status nvarchar(max) NOT NULL, ResolvedBy uniqueidentifier NULL, ResolvedAt datetime2 NULL, ResolutionNotes nvarchar(max) NOT NULL, CONSTRAINT FK_VolunteerCheckIns_Tasks FOREIGN KEY (TaskId) REFERENCES dbo.VolunteerTasks(Id), CONSTRAINT FK_VolunteerCheckIns_Volunteers FOREIGN KEY (VolunteerId) REFERENCES dbo.Volunteers(Id), CONSTRAINT FK_VolunteerCheckIns_Users FOREIGN KEY (ResolvedBy) REFERENCES dbo.Users(Id));
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ShelterAllocations_RequestId' AND object_id = OBJECT_ID(N'dbo.ShelterAllocations')) CREATE UNIQUE INDEX IX_ShelterAllocations_RequestId ON dbo.ShelterAllocations(RequestId);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VolunteerSkills_VolunteerId_Name' AND object_id = OBJECT_ID(N'dbo.VolunteerSkills')) CREATE UNIQUE INDEX IX_VolunteerSkills_VolunteerId_Name ON dbo.VolunteerSkills(VolunteerId, Name);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VolunteerAvailabilities_VolunteerId_StartsAt_EndsAt' AND object_id = OBJECT_ID(N'dbo.VolunteerAvailabilities')) CREATE INDEX IX_VolunteerAvailabilities_VolunteerId_StartsAt_EndsAt ON dbo.VolunteerAvailabilities(VolunteerId, StartsAt, EndsAt);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VolunteerTasks_VolunteerId_ScheduledStartAt' AND object_id = OBJECT_ID(N'dbo.VolunteerTasks')) CREATE INDEX IX_VolunteerTasks_VolunteerId_ScheduledStartAt ON dbo.VolunteerTasks(VolunteerId, ScheduledStartAt);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VolunteerCheckIns_TaskId_At' AND object_id = OBJECT_ID(N'dbo.VolunteerCheckIns')) CREATE INDEX IX_VolunteerCheckIns_TaskId_At ON dbo.VolunteerCheckIns(TaskId, At);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ShelterSupplyRequests_ShelterId_CreatedAt' AND object_id = OBJECT_ID(N'dbo.ShelterSupplyRequests')) CREATE INDEX IX_ShelterSupplyRequests_ShelterId_CreatedAt ON dbo.ShelterSupplyRequests(ShelterId, CreatedAt);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VolunteerQualifications_VolunteerId' AND object_id = OBJECT_ID(N'dbo.VolunteerQualifications')) CREATE INDEX IX_VolunteerQualifications_VolunteerId ON dbo.VolunteerQualifications(VolunteerId);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VolunteerActivityLogs_VolunteerId_At' AND object_id = OBJECT_ID(N'dbo.VolunteerActivityLogs')) CREATE INDEX IX_VolunteerActivityLogs_VolunteerId_At ON dbo.VolunteerActivityLogs(VolunteerId, At);
        """;
        await db.Database.ExecuteSqlRawAsync(sql);
    }

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/alerts", async (AidDbContext db) => await db.PublicAlerts.AsNoTracking().Where(x => x.Status == "Published").OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync());
        api.MapPost("/alerts", async (AlertInput dto, HttpContext ctx, AidDbContext db) => {
            if (dto.Headline?.Trim().Length is not (> 3 and <= 160) || dto.Message?.Trim().Length is not (> 5 and <= 1500) ||
                !new[] { "Advisory", "Warning", "Critical" }.Contains(dto.Severity) || !new[] { "en", "si", "ta" }.Contains(dto.Language) ||
                !await db.Crises.AnyAsync(c => c.Id == dto.CrisisId)) return Results.BadRequest(new { error = "Invalid alert" });
            var a = new PublicAlert { CrisisId = dto.CrisisId, Headline = dto.Headline.Trim(), Message = dto.Message.Trim(), Severity = dto.Severity, Language = dto.Language, PublishedBy = Access.UserId(ctx.User) };
            db.PublicAlerts.Add(a); TraceLog.Add(db, "PublicAlert", a.Id, "Published", Access.UserId(ctx.User));
            await db.SaveChangesAsync(); return Results.Created($"/api/alerts/{a.Id}", a);
        }).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.Admin));
        api.MapGet("/household/me", async (HttpContext ctx, AidDbContext db) => await db.Households.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == Access.UserId(ctx.User)) is { } h ? Results.Ok(h) : Results.NoContent());
        api.MapPut("/household/me", async (HouseholdInput dto, HttpContext ctx, AidDbContext db) => {
            if (dto.Members is < 1 or > 100 || dto.Area?.Length > 120 || dto.Contact?.Length > 120) return Results.BadRequest(new { error = "Invalid household details" });
            var uid = Access.UserId(ctx.User); var h = await db.Households.FirstOrDefaultAsync(x => x.UserId == uid);
            if (h == null) { h = new HouseholdProfile { UserId = uid }; db.Households.Add(h); }
            h.Area = dto.Area?.Trim() ?? ""; h.Members = dto.Members; h.Contact = dto.Contact?.Trim() ?? ""; h.AccessibilityNeeded = dto.AccessibilityNeeded; h.UpdatedAt = DateTime.UtcNow;
            TraceLog.Add(db, "Household", h.Id, "Updated", uid); await db.SaveChangesAsync(); return Results.Ok(h);
        });
        api.MapGet("/feedback/me", async (HttpContext ctx, AidDbContext db) => await db.Feedback.AsNoTracking().Where(x => x.CitizenId == Access.UserId(ctx.User)).OrderByDescending(x => x.CreatedAt).ToListAsync());
        api.MapPost("/feedback", async (FeedbackInput dto, HttpContext ctx, AidDbContext db) => {
            if (dto.Rating is < 1 or > 5 || dto.Comments?.Length > 1200) return Results.BadRequest(new { error = "Rating must be 1-5; comment at most 1200 characters" });
            var uid = Access.UserId(ctx.User); var request = await db.Requests.FirstOrDefaultAsync(x => x.Id == dto.RequestId && x.CitizenId == uid);
            if (request == null) return Results.NotFound();
            if (request.Status is not ("Fulfilled" or "Closed")) return Results.Conflict(new { error = "Feedback can be submitted after fulfilment only" });
            if (await db.Feedback.AnyAsync(x => x.RequestId == dto.RequestId)) return Results.Conflict(new { error = "Feedback already recorded" });
            var f = new AssistanceFeedback { RequestId = dto.RequestId, CitizenId = uid, Rating = dto.Rating, Comments = dto.Comments?.Trim() ?? "" };
            db.Feedback.Add(f); TraceLog.Add(db, "Feedback", f.Id, "Submitted", uid); await db.SaveChangesAsync(); return Results.Created($"/api/feedback/{f.Id}", f);
        });
        api.MapGet("/feedback/summary", async (AidDbContext db) => {
            var all = await db.Feedback.AsNoTracking().ToListAsync();
            return Results.Ok(new { count = all.Count, averageRating = all.Count == 0 ? 0 : Math.Round(all.Average(x => x.Rating), 2) });
        }).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.CaseManager, Roles.Admin));
        api.MapGet("/partners", async (AidDbContext db) => await db.Organisations.AsNoTracking().OrderBy(x => x.Name).ToListAsync())
            .RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.Partner, Roles.Logistics, Roles.Admin));
        api.MapGet("/partners/offers", async (AidDbContext db) => await db.PartnerOffers.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToListAsync())
            .RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.Partner, Roles.Logistics, Roles.Admin));
        api.MapPost("/partners/offers", async (PartnerOfferInput dto, HttpContext ctx, AidDbContext db) => {
            if (dto.Quantity < 1 || dto.ResourceCategory?.Length is not (> 1 and <= 100) || dto.Area?.Length > 120 || dto.Notes?.Length > 1200 ||
                !await db.Organisations.AnyAsync(x => x.Id == dto.OrganisationId)) return Results.BadRequest(new { error = "Invalid partner offer" });
            var uid = Access.UserId(ctx.User); var role = Access.Role(ctx.User);
            var org = await db.Users.Where(x => x.Id == uid).Select(x => x.OrganisationId).FirstOrDefaultAsync();
            if (role == Roles.Partner && org != dto.OrganisationId) return Results.Forbid();
            var offer = new PartnerOffer { OrganisationId = dto.OrganisationId, CreatedBy = uid, ResourceCategory = dto.ResourceCategory.Trim(), Area = dto.Area?.Trim() ?? "", Quantity = dto.Quantity, Notes = dto.Notes?.Trim() ?? "" };
            db.PartnerOffers.Add(offer); TraceLog.Add(db, "PartnerOffer", offer.Id, "Offered", uid); await db.SaveChangesAsync(); return Results.Created($"/api/partners/offers/{offer.Id}", offer);
        }).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.Partner, Roles.Logistics, Roles.Admin));
        api.MapPatch("/partners/offers/{id:guid}/status", async (Guid id, PartnerOfferStatus dto, HttpContext ctx, AidDbContext db) => {
            if (!new[] { "Accepted", "Declined" }.Contains(dto.Status)) return Results.BadRequest(new { error = "Invalid decision" });
            var offer = await db.PartnerOffers.FindAsync(id); if (offer == null) return Results.NotFound();
            if (offer.Status != "Offered") return Results.Conflict(new { error = "Offer already reviewed" });
            offer.Status = dto.Status; TraceLog.Add(db, "PartnerOffer", id, dto.Status, Access.UserId(ctx.User)); await db.SaveChangesAsync(); return Results.Ok(offer);
        }).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.Logistics, Roles.Admin));
        api.MapGet("/insights/forecast", async (AidDbContext db) => {
            var cutoff = DateTime.UtcNow.AddDays(-14);
            var requests = await db.Requests.AsNoTracking().Where(x => x.CreatedAt >= cutoff).ToListAsync();
            var resources = await db.Resources.AsNoTracking().ToListAsync();
            var data = requests.GroupBy(x => x.Category).Select(g => new {
                category = g.Key, last14Days = g.Count(), requestedUnits = g.Sum(x => x.RequestedQuantity),
                estimatedNext7Days = (int)Math.Ceiling(g.Count() / 2.0),
                availableUnits = resources.Where(r => r.Category == g.Key).Sum(r => r.AvailableQuantity),
                method = "Simple historical seven-day baseline; not a trained predictive model"
            }).OrderByDescending(x => x.last14Days);
            return Results.Ok(data);
        }).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.Logistics, Roles.CaseManager, Roles.Admin));
        api.MapGet("/insights/duplicates", async (AidDbContext db) => {
            var recs = await db.AiRecommendations.AsNoTracking().Where(x => x.PossibleDuplicateId != null).OrderByDescending(x => x.CreatedAt).Take(50).ToListAsync();
            return Results.Ok(recs.Select(x => new { x.RequestId, x.PossibleDuplicateId, x.Confidence, x.Rationale, x.ReviewStatus }));
        }).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.CaseManager, Roles.Admin));
        api.MapGet("/insights/anomalies", async (AidDbContext db) => {
            // Transparent heuristic flags for staff review; not fraud determinations.
            var all = await db.Requests.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(500).ToListAsync();
            var flagged = all.Select(x => new { request = x, sameCitizen = all.Count(y => y.Id != x.Id && y.CitizenId == x.CitizenId && Math.Abs((y.CreatedAt - x.CreatedAt).TotalHours) < 24), unusualQuantity = x.RequestedQuantity > Math.Max(50, x.PeopleAffected * 20) })
                .Where(x => x.sameCitizen >= 3 || x.unusualQuantity)
                .Select(x => new { requestId = x.request.Id, category = x.request.Category, createdAt = x.request.CreatedAt,
                    reason = x.unusualQuantity ? "Requested quantity exceeds simple review threshold" : "Multiple requests from the same account in 24 hours",
                    status = "NeedsHumanReview" }).Take(35);
            return Results.Ok(flagged);
        }).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.CaseManager, Roles.Admin));
        api.MapGet("/insights/shortages", async (AidDbContext db) => {
            var pending = await db.Requests.AsNoTracking().Where(x => x.Status == "Verified" || x.Status == "Escalated").ToListAsync();
            var items = await db.Resources.AsNoTracking().ToListAsync();
            return Results.Ok(pending.GroupBy(x => x.Category).Select(g => new {
                category = g.Key, verifiedUnassignedRequests = g.Count(), requiredUnits = g.Sum(x => x.RequestedQuantity),
                availableUnits = items.Where(x => x.Category == g.Key).Sum(x => x.AvailableQuantity),
                shortageUnits = Math.Max(0, g.Sum(x => x.RequestedQuantity) - items.Where(x => x.Category == g.Key).Sum(x => x.AvailableQuantity))
            }).OrderByDescending(x => x.shortageUnits));
        }).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.Logistics, Roles.Admin));
        api.MapGet("/insights/volunteer-matches", async (string skill, AidDbContext db) => {
            if (string.IsNullOrWhiteSpace(skill) || skill.Length > 80) return Results.BadRequest(new { error = "Skill is required" });
            var list = await db.Volunteers.AsNoTracking().Where(x => x.Verified && x.Available).ToListAsync();
            return Results.Ok(list.Where(x => x.Skills.Contains(skill, StringComparison.OrdinalIgnoreCase))
                .Select(x => new { x.Id, x.Area, x.Skills, x.Verified, x.Available, rationale = "Verified, available, matching skill; human assignment required" }));
        }).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.VolunteerCoordinator, Roles.Admin));
    }
    public record PartnerOfferStatus(string Status);
}
