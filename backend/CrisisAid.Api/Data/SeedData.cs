using CrisisAid.Api.Data;
using CrisisAid.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CrisisAid.Api.Services;

public static class SeedData
{
    public static async Task InitialiseAsync(AidDbContext db, IConfiguration cfg)
    {
        await db.Database.EnsureCreatedAsync(); // prototype bootstrap (not production migration strategy)
        if (!cfg.GetValue("Demo:Enabled", false) || await db.Users.AnyAsync()) return;
        var pw = cfg["Demo:Password"];
        if (string.IsNullOrWhiteSpace(pw) || pw.Length < 12)
            throw new InvalidOperationException("Demo:Password (12+ characters) is required when demo seed is enabled.");
        var hasher = new PasswordHasher<UserAccount>();
        var roleNames = new[] { Roles.Citizen, Roles.Volunteer, Roles.Donor, Roles.Coordinator, Roles.CaseManager,
            Roles.Logistics, Roles.ShelterAdmin, Roles.VolunteerCoordinator, Roles.Funding, Roles.Partner, Roles.Admin };
        var roleDefs = roleNames.Select(name => new RoleDefinition { Name = name, Description = "Coursework RBAC: " + name }).ToDictionary(x => x.Name);
        db.RoleDefinitions.AddRange(roleDefs.Values);
        var permissionNames = new[] { "cases.read", "cases.review", "resources.allocate", "shelters.allocate", "volunteers.assign", "donations.review", "audit.read", "users.manage" };
        var permissions = permissionNames.Select(code => new PermissionDefinition { Code = code }).ToDictionary(x => x.Code);
        db.PermissionDefinitions.AddRange(permissions.Values);
        foreach (var name in roleNames)
        {
            var rights = name switch {
                Roles.Admin => permissionNames,
                Roles.Coordinator => permissionNames.Where(x => x != "users.manage").ToArray(),
                Roles.CaseManager => new[] { "cases.read", "cases.review" },
                Roles.Logistics => new[] { "cases.read", "resources.allocate" },
                Roles.ShelterAdmin => new[] { "cases.read", "shelters.allocate" },
                Roles.VolunteerCoordinator => new[] { "cases.read", "volunteers.assign" },
                Roles.Funding => new[] { "donations.review" },
                Roles.Partner => new[] { "cases.read" },
                _ => Array.Empty<string>()
            };
            foreach (var right in rights) db.RolePermissions.Add(new RolePermission {
                RoleDefinitionId = roleDefs[name].Id, PermissionDefinitionId = permissions[right].Id });
        }
        var org = new Organisation { Name = "Coastal Emergency Response", Type = "NGO" };
        db.Organisations.Add(org);
        foreach (var (name, email, role) in new[] {
            ("System Administrator", "admin@demo.local", Roles.Admin),
            ("Response Coordinator", "coordinator@demo.local", Roles.Coordinator),
            ("Field Logistics", "logistics@demo.local", Roles.Logistics),
            ("Shelter Officer", "shelter@demo.local", Roles.ShelterAdmin),
            ("Volunteer Officer", "volunteerlead@demo.local", Roles.VolunteerCoordinator),
            ("Affected Resident", "citizen@demo.local", Roles.Citizen),
            ("Volunteer One", "volunteer@demo.local", Roles.Volunteer),
            ("Donor One", "donor@demo.local", Roles.Donor) })
        {
            var user = new UserAccount { Email = email, DisplayName = name, Role = role,
                RoleId = roleDefs[role].Id,
                OrganisationId = role is "Citizen" or "Donor" ? null : org.Id };
            user.PasswordHash = hasher.HashPassword(user, pw);
            db.Users.Add(user);
            if (role == Roles.Volunteer) db.Volunteers.Add(new VolunteerProfile
                { UserId = user.Id, Area = "Colombo", Skills = "First aid, distribution, transport", Verified = true });
        }
        var crisis = new CrisisEvent { Name = "Western Province Flood Response (DEMO)", Type = "Flood", Area = "Colombo & Gampaha" };
        db.Crises.Add(crisis);
        db.ServiceAreas.Add(new ServiceArea { CrisisId = crisis.Id, Name = "Colombo", Latitude = 6.9271, Longitude = 79.8612 });
        db.Resources.AddRange(
            new ResourceItem { Name = "Emergency food parcels", Category = "Food", Unit = "parcels", AvailableQuantity = 25, Area = "Colombo", OrganisationId = org.Id, Latitude = 6.930, Longitude = 79.865 },
            new ResourceItem { Name = "Bottled drinking water", Category = "Water", Unit = "cases", AvailableQuantity = 14, Area = "Colombo", OrganisationId = org.Id, Latitude = 6.930, Longitude = 79.865 },
            new ResourceItem { Name = "First aid medicine kits", Category = "Medicine", Unit = "kits", AvailableQuantity = 3, Area = "Gampaha", OrganisationId = org.Id, Latitude = 7.09, Longitude = 79.99 });
        db.Shelters.AddRange(
            new Shelter { Name = "Central Community Hall (DEMO)", Area = "Colombo", Capacity = 100, Occupancy = 36, Accessible = true, Facilities = "Water, toilets, ramps", Latitude = 6.919, Longitude = 79.869 },
            new Shelter { Name = "North Relief Centre (DEMO)", Area = "Gampaha", Capacity = 60, Occupancy = 48, Accessible = false, Facilities = "Water, toilets", Latitude = 7.08, Longitude = 79.99 });
        db.Campaigns.Add(new DonationCampaign { Name = "Flood Essentials (DEMO)", CrisisId = crisis.Id, TargetAmount = 500000 });
        var citizen = db.Users.Local.Single(x => x.Email == "citizen@demo.local");
        var coordinator = db.Users.Local.Single(x => x.Email == "coordinator@demo.local");
        var demonstrations = new[] {
            new AssistanceRequest { CitizenId = citizen.Id, CrisisId = crisis.Id, Category = "Water", Description = "Ten displaced residents urgently need clean drinking water near the flood zone (DEMO).", Urgency = "High", PeopleAffected = 10, RequestedQuantity = 5, Latitude = 6.922, Longitude = 79.862, Status = "Submitted" },
            new AssistanceRequest { CitizenId = citizen.Id, CrisisId = crisis.Id, Category = "Food", Description = "Food parcels required for an isolated household after flood evacuation (DEMO).", Urgency = "Medium", PeopleAffected = 4, RequestedQuantity = 4, Latitude = 6.925, Longitude = 79.86, Status = "Verified" },
            new AssistanceRequest { CitizenId = citizen.Id, CrisisId = crisis.Id, Category = "Shelter", Description = "Temporary accessible shelter needed for family displaced by flooding (DEMO).", Urgency = "Critical", PeopleAffected = 5, RequestedQuantity = 5, AccessibilityNeeded = true, Latitude = 6.930, Longitude = 79.866, Status = "Verified" },
            new AssistanceRequest { CitizenId = citizen.Id, CrisisId = crisis.Id, Category = "Medicine", Description = "Medical emergency: injury and medicine required for two people (DEMO).", Urgency = "Critical", PeopleAffected = 2, RequestedQuantity = 2, MedicalEmergency = true, Latitude = 6.935, Longitude = 79.867, Status = "PendingReview" },
            new AssistanceRequest { CitizenId = citizen.Id, CrisisId = crisis.Id, Category = "Food", Description = "Small food replenishment needed at community centre (DEMO).", Urgency = "Low", PeopleAffected = 2, RequestedQuantity = 2, Latitude = 6.91, Longitude = 79.874, Status = "Fulfilled" }
        };
        foreach (var req in demonstrations)
        {
            db.Requests.Add(req);
            db.RequestHistories.Add(new RequestHistory { RequestId = req.Id, ActorId = citizen.Id, Action = "Submitted", Notes = "DEMO data" });
            if (req.Status == "Verified")
            {
                var d = AiDecisionService.Fallback(req);
                db.AiRecommendations.Add(new AiRecommendation { RequestId = req.Id, RecommendedCategory = d.Category,
                    Priority = d.Priority, Confidence = d.Confidence, Rationale = d.Rationale, Model = d.Model,
                    ReviewStatus = "Approved", ReviewedBy = coordinator.Id, ReviewedAt = DateTime.UtcNow,
                    FinalPriority = d.Priority, HumanReason = "DEMO coordinator approved assessment" });
                db.RequestHistories.Add(new RequestHistory { RequestId = req.Id, ActorId = coordinator.Id, Action = "Human review: Approved", Notes = "DEMO verified case" });
            }
            else if (req.Status == "PendingReview")
            {
                var d = AiDecisionService.Fallback(req);
                db.AiRecommendations.Add(new AiRecommendation { RequestId = req.Id, RecommendedCategory = d.Category,
                    Priority = d.Priority, Confidence = d.Confidence, Rationale = d.Rationale, Model = d.Model });
            }
            TraceLog.Add(db, "AssistanceRequest", req.Id, "SeedDemoCase", coordinator.Id, new { req.Category, req.Status });
        }
        await db.SaveChangesAsync();
    }
}
