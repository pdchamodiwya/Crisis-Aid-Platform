using CrisisAid.Api.Data;
using CrisisAid.Api.Domain;
using CrisisAid.Api.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace CrisisAid.Api.Services;

public static class VolunteerOperationsEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var volunteers = api.MapGroup("/volunteers");
        VolunteerProfileEndpoints.Map(api);
        volunteers.MapGet("/me/profile", async (HttpContext ctx, AidDbContext db) =>
        {
            var profile = await db.Volunteers.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == Access.UserId(ctx.User));
            return profile == null ? Results.NoContent() : Results.Ok(new
            {
                profile.Skills, profile.Area, profile.Available, profile.Latitude, profile.Longitude
            });
        }).RequireAuthorization(p => p.RequireRole(Roles.Volunteer));

        volunteers.MapGet("/me/skills", async (HttpContext ctx, AidDbContext db) =>
        {
            var profile = await db.Volunteers.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == Access.UserId(ctx.User));
            if (profile == null) return Results.Ok(Array.Empty<VolunteerSkillResponse>());
            return Results.Ok(await db.VolunteerSkills.AsNoTracking().Where(x => x.VolunteerId == profile.Id)
                .OrderBy(x => x.Name).Select(x => new VolunteerSkillResponse(x.Id, x.Name, x.Verified)).ToListAsync());
        }).RequireAuthorization(p => p.RequireRole(Roles.Volunteer));

        volunteers.MapPut("/me/skills", async (VolunteerSkillsRequest dto, HttpContext ctx, AidDbContext db) =>
        {
            if (dto.Skills == null || dto.Skills.Count > 40 ||
                dto.Skills.Any(x => string.IsNullOrWhiteSpace(x) || x.Trim().Length > 80))
                return Results.BadRequest(new { error = "Provide up to 40 non-empty skills, each at most 80 characters" });
            var userId = Access.UserId(ctx.User);
            var profile = await db.Volunteers.SingleOrDefaultAsync(x => x.UserId == userId);
            if (profile == null)
            {
                profile = new VolunteerProfile { UserId = userId };
                db.Volunteers.Add(profile);
            }
            var skills = dto.Skills.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var old = await db.VolunteerSkills.Where(x => x.VolunteerId == profile.Id).ToListAsync();
            var requestedNames = new HashSet<string>(skills, StringComparer.OrdinalIgnoreCase);
            db.VolunteerSkills.RemoveRange(old.Where(x => !requestedNames.Contains(x.Name)));
            var existingNames = new HashSet<string>(old.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
            db.VolunteerSkills.AddRange(skills.Where(name => !existingNames.Contains(name))
                .Select(name => new VolunteerSkill { VolunteerId = profile.Id, Name = name }));
            profile.Skills = string.Join(", ", skills);
            TraceLog.Add(db, "Volunteer", profile.Id, "SkillsUpdated", userId, new { skills });
            await db.SaveChangesAsync();
            return Results.Ok(skills);
        }).RequireAuthorization(p => p.RequireRole(Roles.Volunteer))
          .WithSummary("Replace a volunteer's structured skills");

        volunteers.MapGet("/{volunteerId:guid}/skills", async (Guid volunteerId, AidDbContext db) =>
        {
            if (!await db.Volunteers.AnyAsync(x => x.Id == volunteerId)) return Results.NotFound();
            return Results.Ok(await db.VolunteerSkills.AsNoTracking().Where(x => x.VolunteerId == volunteerId)
                .OrderBy(x => x.Name).Select(x => new VolunteerSkillResponse(x.Id, x.Name, x.Verified)).ToListAsync());
        }).RequireAuthorization(p => p.RequireRole(Roles.VolunteerCoordinator, Roles.Coordinator, Roles.Admin));

        volunteers.MapPatch("/{volunteerId:guid}/skills/{id:guid}/verify",
            async (Guid volunteerId, Guid id, VolunteerSkillStatusRequest dto, HttpContext ctx, AidDbContext db) =>
            {
                var skill = await db.VolunteerSkills.SingleOrDefaultAsync(x =>
                    x.Id == id && x.VolunteerId == volunteerId);
                if (skill == null) return Results.NotFound();
                skill.Verified = dto.Verified;
                skill.VerifiedBy = dto.Verified ? Access.UserId(ctx.User) : null;
                skill.VerifiedAt = dto.Verified ? DateTime.UtcNow : null;
                TraceLog.Add(db, "VolunteerSkill", id, dto.Verified ? "Verified" : "VerificationRevoked", Access.UserId(ctx.User));
                await db.SaveChangesAsync();
                return Results.Ok(new VolunteerSkillResponse(skill.Id, skill.Name, skill.Verified));
            }).RequireAuthorization(p => p.RequireRole(Roles.VolunteerCoordinator, Roles.Coordinator, Roles.Admin))
              .WithSummary("Verify or revoke a volunteer skill");

        volunteers.MapGet("/me/availability", async (HttpContext ctx, AidDbContext db) =>
        {
            var profile = await db.Volunteers.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == Access.UserId(ctx.User));
            if (profile == null) return Results.Ok(Array.Empty<VolunteerAvailabilityResponse>());
            var rows = await db.VolunteerAvailabilities.AsNoTracking().Where(x => x.VolunteerId == profile.Id)
                .OrderBy(x => x.StartsAt).ToListAsync();
            return Results.Ok(rows.Select(ToAvailabilityResponse));
        }).RequireAuthorization(p => p.RequireRole(Roles.Volunteer));

        volunteers.MapPost("/me/availability", async (VolunteerAvailabilityRequest dto, HttpContext ctx, AidDbContext db) =>
        {
            var start = dto.StartsAt.UtcDateTime;
            var end = dto.EndsAt.UtcDateTime;
            if (start <= DateTime.UtcNow || end <= start || (end - start).TotalDays > 14)
                return Results.BadRequest(new { error = "Availability must be a future interval no longer than 14 days" });
            var profile = await db.Volunteers.SingleOrDefaultAsync(x => x.UserId == Access.UserId(ctx.User));
            if (profile == null) return Results.Conflict(new { error = "Save a volunteer profile before adding availability" });
            var overlaps = await db.VolunteerAvailabilities.AnyAsync(x => x.VolunteerId == profile.Id &&
                x.StartsAt < end && x.EndsAt > start);
            if (overlaps) return Results.Conflict(new { error = "Availability intervals cannot overlap" });
            var availability = new VolunteerAvailability { VolunteerId = profile.Id, StartsAt = start, EndsAt = end };
            db.VolunteerAvailabilities.Add(availability);
            TraceLog.Add(db, "VolunteerAvailability", availability.Id, "Added", profile.UserId);
            await db.SaveChangesAsync();
            return Results.Created($"/api/volunteers/me/availability/{availability.Id}", ToAvailabilityResponse(availability));
        }).RequireAuthorization(p => p.RequireRole(Roles.Volunteer))
          .WithSummary("Add a volunteer availability window");

        volunteers.MapDelete("/me/availability/{id:guid}", async (Guid id, HttpContext ctx, AidDbContext db) =>
        {
            var profile = await db.Volunteers.SingleOrDefaultAsync(x => x.UserId == Access.UserId(ctx.User));
            if (profile == null) return Results.NotFound();
            var availability = await db.VolunteerAvailabilities.SingleOrDefaultAsync(x => x.Id == id && x.VolunteerId == profile.Id);
            if (availability == null) return Results.NotFound();
            db.VolunteerAvailabilities.Remove(availability);
            TraceLog.Add(db, "VolunteerAvailability", id, "Removed", profile.UserId);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(p => p.RequireRole(Roles.Volunteer));

        volunteers.MapGet("/me/qualifications", async (HttpContext ctx, AidDbContext db) =>
        {
            var profile = await db.Volunteers.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == Access.UserId(ctx.User));
            if (profile == null) return Results.Ok(Array.Empty<VolunteerQualificationResponse>());
            var rows = await db.VolunteerQualifications.AsNoTracking().Where(x => x.VolunteerId == profile.Id)
                .OrderBy(x => x.Name).ToListAsync();
            return Results.Ok(rows.Select(ToQualificationResponse));
        }).RequireAuthorization(p => p.RequireRole(Roles.Volunteer));

        volunteers.MapPost("/me/qualifications", async (VolunteerQualificationRequest dto, HttpContext ctx, AidDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length > 120 ||
                dto.Issuer == null || dto.Reference == null || dto.Issuer.Length > 120 || dto.Reference.Length > 240)
                return Results.BadRequest(new { error = "Qualification details are invalid" });
            var profile = await db.Volunteers.SingleOrDefaultAsync(x => x.UserId == Access.UserId(ctx.User));
            if (profile == null) return Results.Conflict(new { error = "Save a volunteer profile before adding qualifications" });
            var qualification = new VolunteerQualification
            {
                VolunteerId = profile.Id, Name = dto.Name.Trim(), Issuer = dto.Issuer.Trim(),
                Reference = dto.Reference.Trim(), ExpiresAt = dto.ExpiresAt?.UtcDateTime
            };
            db.VolunteerQualifications.Add(qualification);
            TraceLog.Add(db, "VolunteerQualification", qualification.Id, "Submitted", profile.UserId);
            await db.SaveChangesAsync();
            return Results.Created($"/api/volunteers/me/qualifications/{qualification.Id}", ToQualificationResponse(qualification));
        }).RequireAuthorization(p => p.RequireRole(Roles.Volunteer))
          .WithSummary("Submit a volunteer qualification for verification");

        volunteers.MapGet("/{volunteerId:guid}/qualifications", async (Guid volunteerId, AidDbContext db) =>
        {
            if (!await db.Volunteers.AnyAsync(x => x.Id == volunteerId)) return Results.NotFound();
            var rows = await db.VolunteerQualifications.AsNoTracking().Where(x => x.VolunteerId == volunteerId)
                .OrderBy(x => x.Name).ToListAsync();
            return Results.Ok(rows.Select(ToQualificationResponse));
        }).RequireAuthorization(p => p.RequireRole(Roles.VolunteerCoordinator, Roles.Coordinator, Roles.Admin));

        volunteers.MapPatch("/{volunteerId:guid}/qualifications/{id:guid}/verify",
            async (Guid volunteerId, Guid id, VolunteerQualificationStatusRequest dto, HttpContext ctx, AidDbContext db) =>
            {
                var qualification = await db.VolunteerQualifications.SingleOrDefaultAsync(x =>
                    x.Id == id && x.VolunteerId == volunteerId);
                if (qualification == null) return Results.NotFound();
                if (dto.Verified && qualification.ExpiresAt is DateTime expires && expires <= DateTime.UtcNow)
                    return Results.Conflict(new { error = "Expired qualifications cannot be verified" });
                qualification.Verified = dto.Verified;
                qualification.VerifiedBy = dto.Verified ? Access.UserId(ctx.User) : null;
                qualification.VerifiedAt = dto.Verified ? DateTime.UtcNow : null;
                TraceLog.Add(db, "VolunteerQualification", id, dto.Verified ? "Verified" : "VerificationRevoked", Access.UserId(ctx.User));
                await db.SaveChangesAsync();
                return Results.Ok(ToQualificationResponse(qualification));
            }).RequireAuthorization(p => p.RequireRole(Roles.VolunteerCoordinator, Roles.Coordinator, Roles.Admin))
              .WithSummary("Verify or revoke a volunteer qualification");

        volunteers.MapPost("/matches", async (VolunteerMatchRequest dto, AidDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(dto.RequiredSkill) || dto.RequiredSkill.Length > 80 ||
                dto.DurationMinutes is <= 0 or > 1440 ||
                (dto.ScheduledStartAt.HasValue != dto.DurationMinutes.HasValue) ||
                (dto.Latitude.HasValue != dto.Longitude.HasValue) ||
                (dto.Latitude.HasValue && !Access.Coordinates(dto.Latitude.Value, dto.Longitude!.Value)))
                return Results.BadRequest(new { error = "Skill, schedule, or coordinates are invalid" });
            var candidates = await db.Volunteers.AsNoTracking().Where(x => x.Verified && x.Available).ToListAsync();
            var candidateIds = candidates.Select(x => x.Id).ToArray();
            var skills = await db.VolunteerSkills.AsNoTracking().Where(x => candidateIds.Contains(x.VolunteerId)).ToListAsync();
            var qualifications = await db.VolunteerQualifications.AsNoTracking()
                .Where(x => candidateIds.Contains(x.VolunteerId) && x.Verified &&
                    (x.ExpiresAt == null || x.ExpiresAt > DateTime.UtcNow)).ToListAsync();
            var windows = await db.VolunteerAvailabilities.AsNoTracking()
                .Where(x => candidateIds.Contains(x.VolunteerId)).ToListAsync();
            var activeTasks = await db.VolunteerTasks.AsNoTracking().Where(x => candidateIds.Contains(x.VolunteerId) &&
                x.Status != "Completed" && x.Status != "Cancelled" && x.Status != "Declined").ToListAsync();
            var ranked = candidates.Select(v => RankVolunteer(v, dto, skills, qualifications, windows, activeTasks))
                .Where(x => x.IsEligible).OrderByDescending(x => x.Score).ToArray();
            return Results.Ok(new
            {
                strategy = "Verified skill 40, verified qualification 15, availability 20, distance 15, workload 10; urgency adjusts workload priority. Distance is straight-line when coordinates are available.",
                candidates = ranked
            });
        }).RequireAuthorization(p => p.RequireRole(Roles.VolunteerCoordinator, Roles.Coordinator, Roles.Admin))
          .WithSummary("Rank volunteers by verified skill, qualification, availability, distance, workload, and urgency");

        volunteers.MapPost("/tasks/{id:guid}/check-ins", async (Guid id, VolunteerCheckInRequest dto,
            HttpContext ctx, AidDbContext db) =>
        {
            if ((dto.Latitude.HasValue != dto.Longitude.HasValue) ||
                (dto.Latitude.HasValue && !Access.Coordinates(dto.Latitude.Value, dto.Longitude!.Value)) ||
                dto.Area == null || dto.Area.Length > 160)
                return Results.BadRequest(new { error = "Check-in coordinates or area are invalid" });
            var task = await db.VolunteerTasks.SingleOrDefaultAsync(x => x.Id == id);
            if (task == null) return Results.NotFound();
            var profile = await db.Volunteers.FindAsync(task.VolunteerId);
            if (profile == null) return Results.NotFound();
            if (profile.UserId != Access.UserId(ctx.User)) return Results.Forbid();
            if (task.Status is not ("Accepted" or "In Progress"))
                return Results.Conflict(new { error = "Check-ins are only available for active assignments" });
            var checkIn = new VolunteerCheckIn
            {
                TaskId = id, VolunteerId = profile.Id, Latitude = dto.Latitude,
                Longitude = dto.Longitude, Area = dto.Area.Trim(), Status = "Received"
            };
            task.NextCheckInDueAt = DateTime.UtcNow.AddMinutes(task.CheckInIntervalMinutes);
            task.OverdueAlertSentAt = null;
            db.VolunteerCheckIns.Add(checkIn);
            db.VolunteerActivityLogs.Add(new VolunteerActivityLog
            {
                VolunteerId = profile.Id, TaskId = task.Id, ActorId = profile.UserId,
                Activity = "SafetyCheckIn", Notes = dto.Area
            });
            TraceLog.Add(db, "VolunteerCheckIn", checkIn.Id, "Received", profile.UserId,
                new { dto.Area, hasLocation = dto.Latitude.HasValue });
            await db.SaveChangesAsync();
            return Results.Created($"/api/volunteers/tasks/{id}/check-ins/{checkIn.Id}", ToCheckInResponse(checkIn));
        }).RequireAuthorization(p => p.RequireRole(Roles.Volunteer))
          .WithSummary("Submit a voluntary safety check-in for an active assignment");

        volunteers.MapGet("/check-ins", async (AidDbContext db) =>
        {
            var rows = await db.VolunteerCheckIns.AsNoTracking().Where(x => x.Status == "Overdue" || x.Status == "Resolved")
                .OrderByDescending(x => x.At).Take(250).ToListAsync();
            return Results.Ok(rows.Select(ToCheckInResponse));
        }).RequireAuthorization(p => p.RequireRole(Roles.VolunteerCoordinator, Roles.Coordinator, Roles.Admin))
          .WithSummary("List overdue safety alerts and their resolution status");

        volunteers.MapGet("/safety-status", async (AidDbContext db) =>
        {
            var tasks = await db.VolunteerTasks.AsNoTracking()
                .Where(x => x.Status == "Accepted" || x.Status == "In Progress")
                .OrderBy(x => x.NextCheckInDueAt).Take(500).ToListAsync();
            var taskIds = tasks.Select(x => x.Id).ToArray();
            var checkIns = await db.VolunteerCheckIns.AsNoTracking().Where(x =>
                    taskIds.Contains(x.TaskId) && x.Status == "Received")
                .OrderByDescending(x => x.At).ToListAsync();
            var checkInsByTask = checkIns.GroupBy(x => x.TaskId).ToDictionary(x => x.Key, x => x.ToArray());
            return Results.Ok(tasks.Select(task =>
            {
                var latest = checkInsByTask.TryGetValue(task.Id, out var history)
                    ? history.FirstOrDefault(x => x.VolunteerId == task.VolunteerId) : null;
                var status = task.OverdueAlertSentAt.HasValue ||
                    (task.NextCheckInDueAt.HasValue && task.NextCheckInDueAt.Value <= DateTime.UtcNow) ? "Check-in overdue" :
                    latest != null ? "Checked in" : "On assignment";
                return new
                {
                    task.Id,
                    task.VolunteerId,
                    task.Status,
                    safetyStatus = status,
                    lastCheckInAt = latest == null ? (DateTimeOffset?)null :
                        new DateTimeOffset(DateTime.SpecifyKind(latest.At, DateTimeKind.Utc)),
                    nextCheckInDueAt = task.NextCheckInDueAt is DateTime due ?
                        new DateTimeOffset(DateTime.SpecifyKind(due, DateTimeKind.Utc)) : (DateTimeOffset?)null,
                    voluntaryArea = latest?.Area,
                    voluntaryLatitude = latest?.Latitude,
                    voluntaryLongitude = latest?.Longitude
                };
            }));
        }).RequireAuthorization(p => p.RequireRole(Roles.VolunteerCoordinator, Roles.Coordinator, Roles.Admin))
          .WithSummary("Show active assignment safety status using only volunteer-submitted check-ins");

        volunteers.MapPatch("/check-ins/{id:guid}/resolve", async (Guid id, VolunteerCheckInResolutionRequest dto,
            HttpContext ctx, AidDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Notes) || dto.Notes.Length > 1000)
                return Results.BadRequest(new { error = "A follow-up resolution note is required" });
            var checkIn = await db.VolunteerCheckIns.SingleOrDefaultAsync(x => x.Id == id && x.Status == "Overdue");
            if (checkIn == null) return Results.NotFound();
            checkIn.Status = "Resolved";
            checkIn.ResolvedBy = Access.UserId(ctx.User);
            checkIn.ResolvedAt = DateTime.UtcNow;
            checkIn.ResolutionNotes = dto.Notes.Trim();
            TraceLog.Add(db, "VolunteerCheckIn", id, "Resolved", Access.UserId(ctx.User));
            await db.SaveChangesAsync();
            return Results.Ok(ToCheckInResponse(checkIn));
        }).RequireAuthorization(p => p.RequireRole(Roles.VolunteerCoordinator, Roles.Coordinator, Roles.Admin))
          .WithSummary("Record coordinator follow-up for an overdue volunteer check-in");
    }

    private static VolunteerMatchResponse RankVolunteer(VolunteerProfile volunteer, VolunteerMatchRequest input,
        IReadOnlyCollection<VolunteerSkill> skills, IReadOnlyCollection<VolunteerQualification> qualifications,
        IReadOnlyCollection<VolunteerAvailability> windows, IReadOnlyCollection<VolunteerTask> tasks)
    {
        var volunteerRows = skills.Where(x => x.VolunteerId == volunteer.Id).ToArray();
        var volunteerSkills = volunteerRows.Where(x => x.Verified).Select(x => x.Name).ToArray();
        var hasSkill = VolunteerSkillMatcher.HasVerifiedSkill(volunteer, volunteerRows, input.RequiredSkill);
        var active = tasks.Where(x => x.VolunteerId == volunteer.Id).ToArray();
        var eligible = hasSkill;
        var reasons = new List<string>();
        if (hasSkill)
            reasons.Add(volunteerRows.Length == 0
                ? $"Legacy profile skill match (unverified): {input.RequiredSkill}"
                : $"Verified skill match: {input.RequiredSkill}");
        else reasons.Add("Required verified skill missing");
        var qualificationMatch = qualifications.Any(x => x.VolunteerId == volunteer.Id &&
            x.Name.Contains(input.RequiredSkill, StringComparison.OrdinalIgnoreCase));
        if (qualificationMatch) reasons.Add("Relevant verified qualification");
        if (input.ScheduledStartAt is DateTimeOffset start && input.DurationMinutes is int duration)
        {
            var startUtc = start.UtcDateTime;
            var endUtc = startUtc.AddMinutes(duration);
            var inWindow = windows.Any(x => x.VolunteerId == volunteer.Id && x.StartsAt <= startUtc && x.EndsAt >= endUtc);
            var conflict = VolunteerSchedule.Overlaps(startUtc, duration, active);
            eligible &= inWindow && !conflict;
            reasons.Add(inWindow ? "Available for requested time" : "No availability window for requested time");
            if (conflict) reasons.Add("Overlapping assignment");
        }
        else
        {
            var conflict = active.Length > 0;
            eligible &= !conflict;
            reasons.Add(conflict ? "Has an active assignment" : "No active assignments");
        }
        double? distance = null;
        var distanceScore = 7;
        if (input.Latitude.HasValue && volunteer.Latitude.HasValue && volunteer.Longitude.HasValue)
        {
            distance = Distances.Km(input.Latitude.Value, input.Longitude!.Value,
                volunteer.Latitude.Value, volunteer.Longitude.Value);
            distanceScore = Math.Max(0, 15 - (int)Math.Round(distance.Value));
            reasons.Add($"{distance.Value:0.0} km straight-line distance");
        }
        else reasons.Add("Precise travel distance unavailable");
        var workloadScore = Math.Max(0, 10 - active.Length * (input.Urgency == "Critical" ? 1 : 3));
        var score = (hasSkill ? 40 : 0) + (qualificationMatch ? 15 : 0) +
            (eligible ? 20 : 0) + distanceScore + workloadScore;
        return new VolunteerMatchResponse(volunteer.Id, volunteer.UserId, volunteer.Area, volunteerSkills,
            score, distance, reasons, eligible);
    }

    private static VolunteerAvailabilityResponse ToAvailabilityResponse(VolunteerAvailability x) =>
        new(x.Id, new DateTimeOffset(DateTime.SpecifyKind(x.StartsAt, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(x.EndsAt, DateTimeKind.Utc)));

    private static VolunteerQualificationResponse ToQualificationResponse(VolunteerQualification x) =>
        new(x.Id, x.Name, x.Issuer, x.Reference, x.Verified,
            x.ExpiresAt is DateTime expiry ? new DateTimeOffset(DateTime.SpecifyKind(expiry, DateTimeKind.Utc)) : null);

    private static VolunteerCheckInResponse ToCheckInResponse(VolunteerCheckIn x) =>
        new(x.Id, x.TaskId, x.VolunteerId, new DateTimeOffset(DateTime.SpecifyKind(x.At, DateTimeKind.Utc)),
            x.Area, x.Latitude, x.Longitude, x.Status, x.ResolutionNotes);
}

public static class VolunteerSchedule
{
    public static bool Overlaps(DateTime startsAt, int durationMinutes, IEnumerable<VolunteerTask> tasks) =>
        tasks.Any(x => x.ScheduledStartAt.HasValue && x.DurationMinutes.HasValue &&
            x.Status is not ("Completed" or "Cancelled" or "Declined") &&
            x.ScheduledStartAt.Value < startsAt.AddMinutes(durationMinutes) &&
            x.ScheduledStartAt.Value.AddMinutes(x.DurationMinutes.Value) > startsAt);
}

public static class VolunteerSkillMatcher
{
    public static bool HasVerifiedSkill(VolunteerProfile profile, IEnumerable<VolunteerSkill> skills, string requiredSkill)
    {
        var rows = skills.ToArray();
        if (rows.Length > 0)
            return rows.Any(x => x.Verified && x.Name.Equals(requiredSkill, StringComparison.OrdinalIgnoreCase));
        return profile.Skills.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(x => x.Equals(requiredSkill, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>Structured skill names to associate with a volunteer profile.</summary>
public sealed record VolunteerSkillsRequest(IReadOnlyList<string> Skills);

/// <summary>Requested volunteer availability window.</summary>
public sealed record VolunteerAvailabilityRequest(DateTimeOffset StartsAt, DateTimeOffset EndsAt);

/// <summary>Volunteer availability window returned to the caller.</summary>
public sealed record VolunteerAvailabilityResponse(Guid Id, DateTimeOffset StartsAt, DateTimeOffset EndsAt);

/// <summary>Qualification supplied by a volunteer for coordinator verification.</summary>
public sealed record VolunteerQualificationRequest(string Name, string Issuer, string Reference, DateTimeOffset? ExpiresAt);

/// <summary>Qualification verification decision.</summary>
public sealed record VolunteerQualificationStatusRequest(bool Verified);

/// <summary>Volunteer skill verification decision.</summary>
public sealed record VolunteerSkillStatusRequest(bool Verified);

/// <summary>Qualification information returned by the API.</summary>
public sealed record VolunteerQualificationResponse(Guid Id, string Name, string Issuer, string Reference,
    bool Verified, DateTimeOffset? ExpiresAt);

/// <summary>Structured skill summary returned by the API.</summary>
public sealed record VolunteerSkillResponse(Guid Id, string Name, bool Verified);

/// <summary>Requirements used to rank eligible volunteers for a task.</summary>
public sealed record VolunteerMatchRequest(string RequiredSkill, DateTimeOffset? ScheduledStartAt,
    int? DurationMinutes, string Urgency = "Medium", double? Latitude = null, double? Longitude = null);

/// <summary>Ranked volunteer candidate with transparent matching reasons.</summary>
public sealed record VolunteerMatchResponse(Guid VolunteerId, Guid UserId, string Area,
    IReadOnlyList<string> Skills, int Score, double? StraightLineDistanceKm, IReadOnlyList<string> Reasons, bool IsEligible);

/// <summary>Voluntary field check-in, with location supplied only when the volunteer chooses.</summary>
public sealed record VolunteerCheckInRequest(string Area = "", double? Latitude = null, double? Longitude = null);

/// <summary>Coordinator follow-up resolution for an overdue check-in.</summary>
public sealed record VolunteerCheckInResolutionRequest(string Notes);

/// <summary>Volunteer check-in or overdue alert record.</summary>
public sealed record VolunteerCheckInResponse(Guid Id, Guid TaskId, Guid VolunteerId, DateTimeOffset At,
    string Area, double? Latitude, double? Longitude, string Status, string ResolutionNotes);
