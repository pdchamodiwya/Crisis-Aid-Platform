using CrisisAid.Api.Data;
using CrisisAid.Api.Domain;
using CrisisAid.Api.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace CrisisAid.Api.Endpoints;

public static class VolunteerProfileEndpoints
{
    private static readonly string[] SkillNames =
    [
        "First aid",
        "Translation",
        "Transportation",
        "Distribution",
        "Administration"
    ];

    public static void Map(RouteGroupBuilder api)
    {
        var volunteers = api.MapGroup("/volunteers");
        volunteers.MapPost("/{volunteerId:guid}/skills",
            async (Guid volunteerId, VolunteerProfileSkillRequest dto, HttpContext ctx, AidDbContext db) =>
            {
                if (dto is null)
                    return Results.BadRequest(new { error = "A skill is required" });
                var name = SkillNames.FirstOrDefault(x =>
                    string.Equals(x, dto.Skill?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (name == null)
                    return Results.BadRequest(new { error = "Choose one of the supported volunteer skills" });

                var profile = await db.Volunteers.SingleOrDefaultAsync(x => x.Id == volunteerId);
                if (profile == null) return Results.NotFound();

                var existing = await db.VolunteerSkills.Where(x => x.VolunteerId == volunteerId).ToListAsync();
                if (existing.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    return Results.Conflict(new { error = "This skill is already recorded for the volunteer" });

                var skill = new VolunteerSkill { VolunteerId = volunteerId, Name = name };
                db.VolunteerSkills.Add(skill);
                var legacySkills = (profile.Skills ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                if (!legacySkills.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    legacySkills.Add(name);
                    profile.Skills = string.Join(", ", legacySkills);
                }

                TraceLog.Add(db, "VolunteerSkill", skill.Id, "AddedUnverified", Access.UserId(ctx.User),
                    new { volunteerId, name });
                await db.SaveChangesAsync();
                return Results.Created($"/api/volunteers/{volunteerId}/skills/{skill.Id}",
                    new VolunteerSkillResponse(skill.Id, skill.Name, skill.Verified));
            }).RequireAuthorization(p => p.RequireRole(Roles.VolunteerCoordinator, Roles.Coordinator, Roles.Admin))
              .WithSummary("Add an unverified supported skill to a volunteer");

        volunteers.MapPost("/{volunteerId:guid}/qualifications",
            async (Guid volunteerId, VolunteerProfileQualificationRequest dto, HttpContext ctx, AidDbContext db) =>
            {
                if (dto is null)
                    return Results.BadRequest(new { error = "Qualification name and issuer are required" });
                var name = dto.Name?.Trim();
                var issuer = dto.Issuer?.Trim();
                if (string.IsNullOrWhiteSpace(name) || name.Length > 120 ||
                    string.IsNullOrWhiteSpace(issuer) || issuer.Length > 120)
                    return Results.BadRequest(new { error = "Qualification name and issuer are required and must be at most 120 characters" });
                if (dto.ExpiryDate is DateOnly expiryDate &&
                    expiryDate < DateOnly.FromDateTime(DateTime.UtcNow))
                    return Results.BadRequest(new { error = "Expiry date cannot be in the past" });

                var profileExists = await db.Volunteers.AnyAsync(x => x.Id == volunteerId);
                if (!profileExists) return Results.NotFound();

                var qualification = new VolunteerQualification
                {
                    VolunteerId = volunteerId,
                    Name = name,
                    Issuer = issuer,
                    Reference = string.Empty,
                    ExpiresAt = dto.ExpiryDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                };
                db.VolunteerQualifications.Add(qualification);
                TraceLog.Add(db, "VolunteerQualification", qualification.Id, "AddedUnverified",
                    Access.UserId(ctx.User), new { volunteerId, name, issuer });
                await db.SaveChangesAsync();
                return Results.Created($"/api/volunteers/{volunteerId}/qualifications/{qualification.Id}",
                    new VolunteerQualificationResponse(qualification.Id, qualification.Name,
                        qualification.Issuer, qualification.Reference, qualification.Verified,
                        qualification.ExpiresAt is DateTime expiry
                            ? new DateTimeOffset(DateTime.SpecifyKind(expiry, DateTimeKind.Utc))
                            : null));
            }).RequireAuthorization(p => p.RequireRole(Roles.VolunteerCoordinator, Roles.Coordinator, Roles.Admin))
              .WithSummary("Add an unverified qualification to a volunteer");

        api.MapPatch("/volunteer-skills/{id:guid}/verify",
            async (Guid id, VolunteerSkillStatusRequest dto, HttpContext ctx, AidDbContext db) =>
            {
                if (dto is null) return Results.BadRequest(new { error = "Verification status is required" });
                var skill = await db.VolunteerSkills.SingleOrDefaultAsync(x => x.Id == id);
                if (skill == null) return Results.NotFound();
                skill.Verified = dto.Verified;
                skill.VerifiedBy = dto.Verified ? Access.UserId(ctx.User) : null;
                skill.VerifiedAt = dto.Verified ? DateTime.UtcNow : null;
                TraceLog.Add(db, "VolunteerSkill", id, dto.Verified ? "Verified" : "VerificationRevoked",
                    Access.UserId(ctx.User));
                await db.SaveChangesAsync();
                return Results.Ok(new VolunteerSkillResponse(skill.Id, skill.Name, skill.Verified));
            }).RequireAuthorization(p => p.RequireRole(Roles.VolunteerCoordinator, Roles.Coordinator, Roles.Admin))
              .WithSummary("Verify or revoke a volunteer skill");

        api.MapPatch("/volunteer-qualifications/{id:guid}/verify",
            async (Guid id, VolunteerQualificationStatusRequest dto, HttpContext ctx, AidDbContext db) =>
            {
                if (dto is null) return Results.BadRequest(new { error = "Verification status is required" });
                var qualification = await db.VolunteerQualifications.SingleOrDefaultAsync(x => x.Id == id);
                if (qualification == null) return Results.NotFound();
                if (dto.Verified && qualification.ExpiresAt is DateTime expires &&
                    expires.Date < DateTime.UtcNow.Date)
                    return Results.Conflict(new { error = "Expired qualifications cannot be verified" });
                qualification.Verified = dto.Verified;
                qualification.VerifiedBy = dto.Verified ? Access.UserId(ctx.User) : null;
                qualification.VerifiedAt = dto.Verified ? DateTime.UtcNow : null;
                TraceLog.Add(db, "VolunteerQualification", id,
                    dto.Verified ? "Verified" : "VerificationRevoked", Access.UserId(ctx.User));
                await db.SaveChangesAsync();
                return Results.Ok(new VolunteerQualificationResponse(qualification.Id, qualification.Name,
                    qualification.Issuer, qualification.Reference, qualification.Verified,
                    qualification.ExpiresAt is DateTime expiry
                        ? new DateTimeOffset(DateTime.SpecifyKind(expiry, DateTimeKind.Utc))
                        : null));
            }).RequireAuthorization(p => p.RequireRole(Roles.VolunteerCoordinator, Roles.Coordinator, Roles.Admin))
              .WithSummary("Verify or revoke a volunteer qualification");
    }
}

public sealed record VolunteerProfileSkillRequest(string Skill);

public sealed record VolunteerProfileQualificationRequest(string Name, string Issuer, DateOnly? ExpiryDate);
