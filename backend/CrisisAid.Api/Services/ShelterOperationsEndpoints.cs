using CrisisAid.Api.Data;
using CrisisAid.Api.Domain;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace CrisisAid.Api.Services;

public static class ShelterOperationsEndpoints
{
    /// <summary>Maps shelter stays, facility directory, recommendations, and supply requests.</summary>
    public static void Map(RouteGroupBuilder api)
    {
        var shelters = api.MapGroup("/shelters");
        shelters.MapPut("/{id:guid}", async (Guid id, ShelterUpdateRequest dto, HttpContext ctx, AidDbContext db) =>
        {
            if (!Valid(dto)) return Results.BadRequest(new { error = "Shelter details or coordinates are invalid" });
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var shelter = await ShelterConcurrencyLock.AcquireAsync(db, id);
            if (shelter == null) return Results.NotFound();
            if (!CanManage(shelter, ctx.User)) return Results.Forbid();
            var reserved = await db.ShelterAllocations.Where(x => x.ShelterId == id && x.Status == "Reserved")
                .SumAsync(x => (int?)x.PeopleCount) ?? 0;
            if (dto.Capacity < shelter.Occupancy + reserved)
                return Results.Conflict(new { error = "Capacity cannot be lower than current occupants plus reservations" });
            shelter.Name = dto.Name.Trim();
            shelter.Area = dto.Area.Trim();
            shelter.Capacity = dto.Capacity;
            shelter.Accessible = dto.Accessible;
            shelter.Facilities = dto.Facilities.Trim();
            shelter.Latitude = dto.Latitude;
            shelter.Longitude = dto.Longitude;
            shelter.ContactName = dto.ContactName.Trim();
            shelter.ContactPhone = dto.ContactPhone.Trim();
            shelter.ContactEmail = dto.ContactEmail.Trim();
            shelter.OpeningHours = dto.OpeningHours.Trim();
            shelter.MedicalFacilities = dto.MedicalFacilities;
            shelter.SanitationFacilities = dto.SanitationFacilities;
            shelter.FoodAvailable = dto.FoodAvailable;
            if (shelter.AdministratorId == null && Access.Role(ctx.User) == Roles.ShelterAdmin)
                shelter.AdministratorId = Access.UserId(ctx.User);
            TraceLog.Add(db, "Shelter", id, "Updated", Access.UserId(ctx.User));
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(ToFacility(shelter, reserved));
        }).RequireAuthorization(p => p.RequireRole(Roles.ShelterAdmin, Roles.Coordinator, Roles.Admin))
          .WithSummary("Update shelter information and facilities");

        shelters.MapPatch("/{id:guid}/status", async (Guid id, ShelterStatusRequest dto, HttpContext ctx, AidDbContext db) =>
        {
            if (!new[] { "Open", "Closed", "Maintenance" }.Contains(dto.Status, StringComparer.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = "Status must be Open, Closed, or Maintenance" });
            var shelter = await db.Shelters.FindAsync(id);
            if (shelter == null) return Results.NotFound();
            if (!CanManage(shelter, ctx.User)) return Results.Forbid();
            shelter.Status = dto.Status.Equals("Open", StringComparison.OrdinalIgnoreCase) ? "Open" :
                dto.Status.Equals("Closed", StringComparison.OrdinalIgnoreCase) ? "Closed" : "Maintenance";
            TraceLog.Add(db, "Shelter", id, "StatusChanged", Access.UserId(ctx.User), new { shelter.Status });
            await db.SaveChangesAsync();
            return Results.Ok(new { shelter.Id, shelter.Status });
        }).RequireAuthorization(p => p.RequireRole(Roles.ShelterAdmin, Roles.Coordinator, Roles.Admin))
          .WithSummary("Change shelter operating status");

        shelters.MapPatch("/{id:guid}/administrator", async (Guid id, ShelterAdministratorRequest dto,
            HttpContext ctx, AidDbContext db) =>
        {
            var shelter = await db.Shelters.FindAsync(id);
            if (shelter == null) return Results.NotFound();
            if (dto.UserId is Guid administratorId)
            {
                var administrator = await db.Users.AsNoTracking().SingleOrDefaultAsync(x =>
                    x.Id == administratorId && x.IsActive && x.Role == Roles.ShelterAdmin);
                if (administrator == null)
                    return Results.BadRequest(new { error = "Administrator must be an active ShelterAdministrator user" });
            }
            shelter.AdministratorId = dto.UserId;
            TraceLog.Add(db, "Shelter", id, "AdministratorAssigned", Access.UserId(ctx.User),
                new { dto.UserId });
            await db.SaveChangesAsync();
            return Results.Ok(new { shelter.Id, shelter.AdministratorId });
        }).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.Admin))
          .WithSummary("Assign or clear the responsible shelter administrator");

        shelters.MapGet("/{id:guid}/occupancy", async (Guid id, AidDbContext db) =>
        {
            var shelter = await db.Shelters.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (shelter == null) return Results.NotFound();
            var reserved = await db.ShelterAllocations.AsNoTracking()
                .Where(x => x.ShelterId == id && x.Status == "Reserved").SumAsync(x => (int?)x.PeopleCount) ?? 0;
            return Results.Ok(new
            {
                shelterId = id,
                capacity = shelter.Capacity,
                occupied = shelter.Occupancy,
                reserved,
                available = ShelterCapacity.Remaining(shelter.Capacity, shelter.Occupancy, reserved)
            });
        }).WithSummary("Get actual occupancy and reserved shelter capacity");

        api.MapPost("/shelter-allocations/{id:guid}/check-in", async (Guid id, HttpContext ctx, AidDbContext db) =>
        {
            var shelterId = await db.ShelterAllocations.AsNoTracking().Where(x => x.Id == id)
                .Select(x => (Guid?)x.ShelterId).SingleOrDefaultAsync();
            if (shelterId == null) return Results.NotFound();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var shelter = await ShelterConcurrencyLock.AcquireAsync(db, shelterId.Value);
            if (shelter == null) return Results.NotFound();
            var allocation = await db.ShelterAllocations.SingleOrDefaultAsync(x => x.Id == id);
            if (allocation == null) return Results.NotFound();
            if (allocation.Status != "Reserved") return Results.Conflict(new { error = "Only a reserved allocation can check in" });
            if (!CanManage(shelter, ctx.User)) return Results.Forbid();
            var otherReservations = await db.ShelterAllocations.Where(x => x.ShelterId == shelter.Id &&
                    x.Status == "Reserved" && x.Id != allocation.Id)
                .SumAsync(x => (int?)x.PeopleCount) ?? 0;
            if (shelter.Status != "Open" || shelter.Occupancy + otherReservations + allocation.PeopleCount > shelter.Capacity)
                return Results.Conflict(new { error = "Shelter is closed or no longer has sufficient capacity" });
            var request = await db.Requests.SingleAsync(x => x.Id == allocation.RequestId);
            var now = DateTime.UtcNow;
            shelter.Occupancy += allocation.PeopleCount;
            allocation.Status = "CheckedIn";
            var stay = new ShelterStay
            {
                AllocationId = allocation.Id,
                HouseholdId = request.CitizenId,
                CheckInAt = now,
                OccupancyCount = allocation.PeopleCount,
                Status = "CheckedIn"
            };
            db.ShelterStays.Add(stay);
            TraceLog.Add(db, "ShelterAllocation", id, "CheckedIn", Access.UserId(ctx.User),
                new { allocation.PeopleCount, stay.HouseholdId });
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(new ShelterStayResponse(stay.Id, id, stay.HouseholdId,
                new DateTimeOffset(now, TimeSpan.Zero), null, stay.OccupancyCount, stay.Status));
        }).RequireAuthorization(p => p.RequireRole(Roles.ShelterAdmin, Roles.Coordinator, Roles.Admin))
          .WithSummary("Check a reserved household into a shelter");

        api.MapPost("/shelter-allocations/{id:guid}/check-out", async (Guid id, HttpContext ctx, AidDbContext db) =>
        {
            var shelterId = await db.ShelterAllocations.AsNoTracking().Where(x => x.Id == id)
                .Select(x => (Guid?)x.ShelterId).SingleOrDefaultAsync();
            if (shelterId == null) return Results.NotFound();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var shelter = await ShelterConcurrencyLock.AcquireAsync(db, shelterId.Value);
            if (shelter == null) return Results.NotFound();
            var allocation = await db.ShelterAllocations.SingleOrDefaultAsync(x => x.Id == id);
            if (allocation == null) return Results.NotFound();
            if (allocation.Status != "CheckedIn") return Results.Conflict(new { error = "Only a checked-in allocation can check out" });
            if (!CanManage(shelter, ctx.User)) return Results.Forbid();
            var request = await db.Requests.SingleAsync(x => x.Id == allocation.RequestId);
            var stay = await db.ShelterStays.SingleOrDefaultAsync(x => x.AllocationId == id);
            var now = DateTime.UtcNow;
            if (stay == null)
            {
                stay = new ShelterStay { AllocationId = id, HouseholdId = request.CitizenId,
                    CheckInAt = allocation.At, OccupancyCount = allocation.PeopleCount, Status = "CheckedIn" };
                db.ShelterStays.Add(stay);
            }
            if (shelter.Occupancy < stay.OccupancyCount)
                return Results.Conflict(new { error = "Shelter occupancy is inconsistent; manual reconciliation is required" });
            shelter.Occupancy -= stay.OccupancyCount;
            stay.CheckOutAt = now;
            stay.Status = "CheckedOut";
            allocation.Status = "CheckedOut";
            TraceLog.Add(db, "ShelterAllocation", id, "CheckedOut", Access.UserId(ctx.User),
                new { stay.OccupancyCount });
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(new ShelterStayResponse(stay.Id, id, stay.HouseholdId,
                stay.CheckInAt is DateTime checkIn ? new DateTimeOffset(DateTime.SpecifyKind(checkIn, DateTimeKind.Utc)) : null,
                new DateTimeOffset(now, TimeSpan.Zero), stay.OccupancyCount, stay.Status));
        }).RequireAuthorization(p => p.RequireRole(Roles.ShelterAdmin, Roles.Coordinator, Roles.Admin))
          .WithSummary("Check a household out and release occupied capacity");

        api.MapPost("/shelter-allocations/{id:guid}/cancel", async (Guid id, HttpContext ctx, AidDbContext db) =>
        {
            var shelterId = await db.ShelterAllocations.AsNoTracking().Where(x => x.Id == id)
                .Select(x => (Guid?)x.ShelterId).SingleOrDefaultAsync();
            if (shelterId == null) return Results.NotFound();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var shelter = await ShelterConcurrencyLock.AcquireAsync(db, shelterId.Value);
            if (shelter == null) return Results.NotFound();
            var allocation = await db.ShelterAllocations.SingleOrDefaultAsync(x => x.Id == id);
            if (allocation == null) return Results.NotFound();
            if (!CanManage(shelter, ctx.User)) return Results.Forbid();
            if (allocation.Status != "Reserved") return Results.Conflict(new { error = "Only a reservation can be cancelled" });
            allocation.Status = "Cancelled";
            var request = await db.Requests.SingleAsync(x => x.Id == allocation.RequestId);
            if (request.Status == "Assigned" &&
                !await db.ResourceAllocations.AnyAsync(x => x.RequestId == request.Id && x.Status != "Cancelled"))
                request.Status = "Verified";
            TraceLog.Add(db, "ShelterAllocation", id, "Cancelled", Access.UserId(ctx.User));
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(new { allocation.Id, allocation.Status });
        }).RequireAuthorization(p => p.RequireRole(Roles.ShelterAdmin, Roles.Coordinator, Roles.Admin))
          .WithSummary("Cancel a reservation and release reserved capacity");

        shelters.MapGet("/facilities", async (AidDbContext db) =>
        {
            var sheltersList = await db.Shelters.AsNoTracking().ToListAsync();
            var centreList = await db.AssistanceCentres.AsNoTracking().ToListAsync();
            var reservedByShelter = await db.ShelterAllocations.AsNoTracking().Where(x => x.Status == "Reserved")
                .GroupBy(x => x.ShelterId).Select(g => new { ShelterId = g.Key, Count = g.Sum(x => x.PeopleCount) })
                .ToDictionaryAsync(x => x.ShelterId, x => x.Count);
            var items = sheltersList.Select(x => ToFacility(x, reservedByShelter.GetValueOrDefault(x.Id)))
                .Concat(centreList.Select(ToFacility));
            return Results.Ok(items);
        }).WithSummary("List shelters and other assistance centres in one directory");

        api.MapPost("/assistance-centres", async (AssistanceCentreRequest dto, HttpContext ctx, AidDbContext db) =>
        {
            if (!Valid(dto)) return Results.BadRequest(new { error = "Assistance centre details or coordinates are invalid" });
            var centre = new AssistanceCentre
            {
                Name = dto.Name.Trim(), Type = dto.Type.Trim(), Area = dto.Area.Trim(),
                Facilities = dto.Facilities.Trim(), Accessible = dto.Accessible,
                ContactName = dto.ContactName.Trim(), ContactPhone = dto.ContactPhone.Trim(),
                ContactEmail = dto.ContactEmail.Trim(), OpeningHours = dto.OpeningHours.Trim(),
                Latitude = dto.Latitude, Longitude = dto.Longitude, AdministratorId = Access.UserId(ctx.User)
            };
            db.AssistanceCentres.Add(centre);
            TraceLog.Add(db, "AssistanceCentre", centre.Id, "Created", Access.UserId(ctx.User));
            await db.SaveChangesAsync();
            return Results.Created($"/api/assistance-centres/{centre.Id}", ToFacility(centre));
        }).RequireAuthorization(p => p.RequireRole(Roles.ShelterAdmin, Roles.Coordinator, Roles.Admin))
          .WithSummary("Add a facility to the assistance-centre directory");

        api.MapPut("/assistance-centres/{id:guid}", async (Guid id, AssistanceCentreRequest dto, HttpContext ctx, AidDbContext db) =>
        {
            if (!Valid(dto)) return Results.BadRequest(new { error = "Assistance centre details or coordinates are invalid" });
            var centre = await db.AssistanceCentres.FindAsync(id);
            if (centre == null) return Results.NotFound();
            if (!CanManage(centre, ctx.User)) return Results.Forbid();
            centre.Name = dto.Name.Trim(); centre.Type = dto.Type.Trim(); centre.Area = dto.Area.Trim();
            centre.Facilities = dto.Facilities.Trim(); centre.Accessible = dto.Accessible;
            centre.ContactName = dto.ContactName.Trim(); centre.ContactPhone = dto.ContactPhone.Trim();
            centre.ContactEmail = dto.ContactEmail.Trim(); centre.OpeningHours = dto.OpeningHours.Trim();
            centre.Latitude = dto.Latitude; centre.Longitude = dto.Longitude;
            TraceLog.Add(db, "AssistanceCentre", id, "Updated", Access.UserId(ctx.User));
            await db.SaveChangesAsync();
            return Results.Ok(ToFacility(centre));
        }).RequireAuthorization(p => p.RequireRole(Roles.ShelterAdmin, Roles.Coordinator, Roles.Admin))
          .WithSummary("Update an assistance centre");

        api.MapPost("/shelters/recommendations", async (ShelterRecommendationRequest dto, HttpContext ctx, AidDbContext db,
            IShelterRecommendationService recommendationService) =>
        {
            var request = await db.Requests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == dto.RequestId);
            if (request == null) return Results.NotFound();
            if (request.Status is not ("Verified" or "Assigned"))
                return Results.Conflict(new { error = "Shelter recommendations require a human-reviewed request" });
            var sheltersList = await db.Shelters.AsNoTracking().Where(x => x.Status == "Open").ToListAsync();
            var reserved = await db.ShelterAllocations.AsNoTracking().Where(x => x.Status == "Reserved")
                .GroupBy(x => x.ShelterId).Select(g => new { ShelterId = g.Key, Count = g.Sum(x => x.PeopleCount) })
                .ToDictionaryAsync(x => x.ShelterId, x => x.Count);
            var ranked = recommendationService.Rank(request, sheltersList, reserved,
                dto.RequireMedical || request.MedicalEmergency, dto.RequireSanitation, dto.RequireFood);
            foreach (var item in ranked)
                TraceLog.Add(db, "ShelterRecommendation", item.Id, "Ranked", Access.UserId(ctx.User),
                    new { request.Id, item.Score, item.Reasons, strategy = "capacity/accessibility/facilities/straight-line-distance-v1" });
            await db.SaveChangesAsync();
            return Results.Ok(new
            {
                strategy = ShelterRecommendationService.StrategyDescription,
                recommendations = ranked
            });
        }).RequireAuthorization(p => p.RequireRole(Roles.ShelterAdmin, Roles.Coordinator, Roles.Admin))
          .WithSummary("Rank suitable open shelters and audit each recommendation");

        api.MapPost("/shelters/{id:guid}/supply-requests", async (Guid id, ShelterSupplyRequestInput dto, HttpContext ctx, AidDbContext db) =>
        {
            if (dto.Quantity < 1 || string.IsNullOrWhiteSpace(dto.Description) || dto.Description.Length > 1000 ||
                string.IsNullOrWhiteSpace(dto.Category) ||
                !new[] { "Food", "Water", "Medicine", "Other" }.Contains(dto.Category, StringComparer.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = "Supply category, quantity, or description is invalid" });
            var shelter = await db.Shelters.FindAsync(id);
            if (shelter == null) return Results.NotFound();
            if (!CanManage(shelter, ctx.User)) return Results.Forbid();
            if (!await db.Crises.AnyAsync(x => x.Id == dto.CrisisId))
                return Results.BadRequest(new { error = "Crisis was not found" });
            var category = new[] { "Food", "Water", "Medicine", "Other" }
                .First(x => x.Equals(dto.Category, StringComparison.OrdinalIgnoreCase));
            var supply = new ShelterSupplyRequest
            {
                ShelterId = id, RequestedBy = Access.UserId(ctx.User), CrisisId = dto.CrisisId,
                Category = category, Description = dto.Description.Trim(), Quantity = dto.Quantity
            };
            db.ShelterSupplyRequests.Add(supply);
            TraceLog.Add(db, "ShelterSupplyRequest", supply.Id, "Pending", supply.RequestedBy,
                new { supply.Category, supply.Quantity });
            await db.SaveChangesAsync();
            return Results.Created($"/api/shelter-supply-requests/{supply.Id}", ToSupplyResponse(supply));
        }).RequireAuthorization(p => p.RequireRole(Roles.ShelterAdmin, Roles.Coordinator, Roles.Admin))
          .WithSummary("Request food, medicine, water, or other shelter resources");

        api.MapGet("/shelter-supply-requests", async (HttpContext ctx, AidDbContext db) =>
        {
            var query = db.ShelterSupplyRequests.AsNoTracking().AsQueryable();
            if (Access.Role(ctx.User) == Roles.ShelterAdmin)
            {
                var actorId = Access.UserId(ctx.User);
                query = query.Where(x => x.RequestedBy == actorId ||
                    db.Shelters.Any(s => s.Id == x.ShelterId && s.AdministratorId == actorId));
            }
            var records = await query.OrderByDescending(x => x.CreatedAt).Take(250).ToListAsync();
            return Results.Ok(records.Select(ToSupplyResponse));
        }).RequireAuthorization(p => p.RequireRole(Roles.ShelterAdmin, Roles.Logistics, Roles.Coordinator, Roles.Admin))
          .WithSummary("List shelter supply requests for shelter and logistics staff");

        api.MapPatch("/shelter-supply-requests/{id:guid}/status", async (Guid id, ShelterSupplyStatusRequest dto,
            HttpContext ctx, AidDbContext db) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var actorRole = Access.Role(ctx.User);
            var isLogistics = actorRole is Roles.Logistics or Roles.Coordinator or Roles.Admin;
            var supply = await db.ShelterSupplyRequests.SingleOrDefaultAsync(x => x.Id == id);
            if (supply == null) return Results.NotFound();
            if (dto.Status == "Approved" || dto.Status == "Rejected")
            {
                if (!new[] { Roles.ShelterAdmin, Roles.Coordinator, Roles.Admin }.Contains(actorRole))
                    return Results.Forbid();
                var shelter = await db.Shelters.SingleAsync(x => x.Id == supply.ShelterId);
                if (!CanManage(shelter, ctx.User)) return Results.Forbid();
                if (supply.Status != "Pending") return Results.Conflict(new { error = "Only pending requests can be reviewed" });
                if (dto.Status == "Approved")
                {
                    var linked = new AssistanceRequest
                    {
                        CitizenId = supply.RequestedBy, CrisisId = supply.CrisisId, Category = supply.Category,
                        Description = $"Shelter supply request for {shelter.Name}: {supply.Description}",
                        Urgency = "Medium", PeopleAffected = 1, RequestedQuantity = supply.Quantity,
                        Latitude = shelter.Latitude, Longitude = shelter.Longitude, Status = "Verified"
                    };
                    db.Requests.Add(linked);
                    supply.AssistanceRequestId = linked.Id;
                    var logisticsUsers = await db.Users.Where(x => x.IsActive && x.Role == Roles.Logistics)
                        .Select(x => x.Id).ToListAsync();
                    foreach (var recipientId in logisticsUsers)
                        TraceLog.Queue(db, recipientId, $"shelter-supply:{supply.Id:N}:approved:{recipientId:N}",
                            "Shelter supply request approved",
                            $"{supply.Quantity} units of {supply.Category} are ready in the logistics request queue.");
                }
                supply.Status = dto.Status;
            }
            else if (dto.Status is "Dispatched" or "Delivered")
            {
                if (!isLogistics) return Results.Forbid();
                if (supply.Status is not ("Approved" or "Dispatched"))
                    return Results.Conflict(new { error = "Supply request must be approved before logistics fulfillment" });
                supply.Status = dto.Status;
                if (supply.AssistanceRequestId is Guid linkedId)
                {
                    var linked = await db.Requests.FindAsync(linkedId);
                    if (linked != null) linked.Status = dto.Status == "Delivered" ? "Fulfilled" : "Assigned";
                }
            }
            else return Results.BadRequest(new { error = "Status must be Approved, Rejected, Dispatched, or Delivered" });
            supply.UpdatedAt = DateTime.UtcNow;
            TraceLog.Add(db, "ShelterSupplyRequest", id, supply.Status, Access.UserId(ctx.User), new { supply.AssistanceRequestId });
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(ToSupplyResponse(supply));
        }).RequireAuthorization(p => p.RequireRole(Roles.ShelterAdmin, Roles.Logistics, Roles.Coordinator, Roles.Admin))
          .WithSummary("Review a shelter supply request or track logistics fulfillment");
    }

    private static bool Valid(ShelterUpdateRequest dto) =>
        !string.IsNullOrWhiteSpace(dto.Name) && dto.Name.Length <= 160 &&
        !string.IsNullOrWhiteSpace(dto.Area) && dto.Area.Length <= 160 &&
        dto.Facilities != null && dto.ContactName != null && dto.ContactPhone != null &&
        dto.ContactEmail != null && dto.OpeningHours != null &&
        dto.Capacity > 0 && Access.Coordinates(dto.Latitude, dto.Longitude) &&
        dto.ContactEmail.Length <= 240;

    private static bool Valid(AssistanceCentreRequest dto) =>
        !string.IsNullOrWhiteSpace(dto.Name) && dto.Name.Length <= 160 &&
        !string.IsNullOrWhiteSpace(dto.Type) && dto.Type.Length <= 80 &&
        !string.IsNullOrWhiteSpace(dto.Area) && dto.Area.Length <= 160 &&
        dto.Facilities != null && dto.ContactName != null && dto.ContactPhone != null &&
        dto.ContactEmail != null && dto.OpeningHours != null &&
        Access.Coordinates(dto.Latitude, dto.Longitude) && dto.ContactEmail.Length <= 240;

    private static bool CanManage(Shelter shelter, System.Security.Claims.ClaimsPrincipal user) =>
        Access.Role(user) is Roles.Coordinator or Roles.Admin ||
        (Access.Role(user) == Roles.ShelterAdmin &&
            (shelter.AdministratorId == null || shelter.AdministratorId == Access.UserId(user)));

    private static bool CanManage(AssistanceCentre centre, System.Security.Claims.ClaimsPrincipal user) =>
        Access.Role(user) is Roles.Coordinator or Roles.Admin ||
        (Access.Role(user) == Roles.ShelterAdmin &&
            (centre.AdministratorId == null || centre.AdministratorId == Access.UserId(user)));

    private static FacilityResponse ToFacility(Shelter s, int reserved) => new(s.Id, "Shelter", s.Name,
        s.Area, s.Status, s.Accessible, s.Facilities, s.Latitude, s.Longitude, s.ContactName, s.ContactPhone,
        s.ContactEmail, s.OpeningHours, s.Capacity, s.Occupancy, reserved,
        ShelterCapacity.Remaining(s.Capacity, s.Occupancy, reserved), s.MedicalFacilities, s.SanitationFacilities, s.FoodAvailable);

    private static FacilityResponse ToFacility(AssistanceCentre c) => new(c.Id, c.Type, c.Name, c.Area,
        c.Status, c.Accessible, c.Facilities, c.Latitude, c.Longitude, c.ContactName, c.ContactPhone,
        c.ContactEmail, c.OpeningHours, null, null, null, null, false, false, false);

    private static ShelterSupplyResponse ToSupplyResponse(ShelterSupplyRequest s) =>
        new(s.Id, s.ShelterId, s.CrisisId, s.Category, s.Description, s.Quantity, s.Status,
            s.AssistanceRequestId, new DateTimeOffset(DateTime.SpecifyKind(s.CreatedAt, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(s.UpdatedAt, DateTimeKind.Utc)));
}

public static class ShelterCapacity
{
    public static int Remaining(int capacity, int occupied, int reserved) =>
        Math.Max(0, capacity - occupied - reserved);

    public static bool CanReserve(int capacity, int occupied, int reserved, int people) =>
        people > 0 && people <= Remaining(capacity, occupied, reserved);

    public static bool IsSuitable(AssistanceRequest request, Shelter shelter, int reserved,
        bool requireMedical, bool requireSanitation, bool requireFood) =>
        shelter.Status == "Open" && CanReserve(shelter.Capacity, shelter.Occupancy, reserved, request.PeopleAffected) &&
        (!request.AccessibilityNeeded || shelter.Accessible) &&
        (!requireMedical || shelter.MedicalFacilities) &&
        (!requireSanitation || shelter.SanitationFacilities) &&
        (!requireFood || shelter.FoodAvailable);
}

public static class ShelterConcurrencyLock
{
    // Hold a per-shelter update lock while capacity-changing transactions inspect allocations.
    public static Task<Shelter?> AcquireAsync(AidDbContext db, Guid id) =>
        db.Shelters.FromSqlInterpolated(
            $"SELECT * FROM [dbo].[Shelters] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}")
            .SingleOrDefaultAsync();
}

/// <summary>Complete shelter details for editing.</summary>
public sealed record ShelterUpdateRequest(string Name, string Area, int Capacity, bool Accessible, string Facilities,
    double Latitude, double Longitude, string ContactName, string ContactPhone, string ContactEmail,
    string OpeningHours, bool MedicalFacilities, bool SanitationFacilities, bool FoodAvailable);

/// <summary>Requested shelter operating state.</summary>
public sealed record ShelterStatusRequest(string Status);

/// <summary>Assigns an active shelter administrator or clears the assignment.</summary>
public sealed record ShelterAdministratorRequest(Guid? UserId);

/// <summary>Represents a facility in the combined shelter and assistance-centre directory.</summary>
public sealed record FacilityResponse(Guid Id, string Type, string Name, string Area, string Status, bool Accessible,
    string Facilities, double Latitude, double Longitude, string ContactName, string ContactPhone, string ContactEmail,
    string OpeningHours, int? Capacity, int? Occupancy, int? Reserved, int? Available,
    bool MedicalFacilities, bool SanitationFacilities, bool FoodAvailable);

/// <summary>Check-in and check-out record for a shelter allocation.</summary>
public sealed record ShelterStayResponse(Guid Id, Guid AllocationId, Guid HouseholdId, DateTimeOffset? CheckInAt,
    DateTimeOffset? CheckOutAt, int OccupancyCount, string Status);

/// <summary>Information for an assistance-centre directory entry.</summary>
public sealed record AssistanceCentreRequest(string Name, string Type, string Area, string Facilities, bool Accessible,
    string ContactName, string ContactPhone, string ContactEmail, string OpeningHours, double Latitude, double Longitude);

/// <summary>Request to rank suitable shelters for a reviewed household request.</summary>
public sealed record ShelterRecommendationRequest(Guid RequestId, bool RequireMedical = false,
    bool RequireSanitation = false, bool RequireFood = false);

/// <summary>Ranked shelter recommendation and its auditable suitability reasons.</summary>
public sealed record ShelterRecommendationResponse(Guid Id, string Name, string Area, int Score,
    int Available, int Capacity, int Occupancy, int Reserved, double StraightLineDistanceKm,
    IReadOnlyList<string> Reasons, bool IsSuitable)
{
    public Guid ShelterId => Id;
    public int MatchScore => Score;
    public int FreeSpaces => Available;
    public double DistanceKm => StraightLineDistanceKm;
    public int Occupied => Occupancy;
    public int TotalBeds => Capacity;
}

/// <summary>Request to add supplies to a shelter's resource workflow.</summary>
public sealed record ShelterSupplyRequestInput(Guid CrisisId, string Category, string Description, int Quantity);

/// <summary>Requested logistics state for an approved shelter supply request.</summary>
public sealed record ShelterSupplyStatusRequest(string Status);

/// <summary>Supply request linked to the existing assistance and logistics workflow.</summary>
public sealed record ShelterSupplyResponse(Guid Id, Guid ShelterId, Guid CrisisId, string Category, string Description,
    int Quantity, string Status, Guid? AssistanceRequestId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
