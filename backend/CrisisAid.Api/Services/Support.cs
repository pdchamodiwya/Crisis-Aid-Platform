using System.Security.Claims;
using System.Text.Json;
using CrisisAid.Api.Data;
using CrisisAid.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrisisAid.Api.Services;

public static class Access
{
    public static Guid UserId(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public static string Role(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.Role) ?? "";
    public static bool Staff(ClaimsPrincipal principal) => Roles.Staff.Split(',').Contains(Role(principal));

        public static bool InRoles(ClaimsPrincipal principal, params string[] roles) => roles.Contains(Role(principal));
    public static bool CanManageVolunteers(ClaimsPrincipal principal) =>
        InRoles(principal, Roles.VolunteerCoordinator, Roles.Coordinator, Roles.Admin);
    public static bool CanSeeAllDonations(ClaimsPrincipal principal) =>
        InRoles(principal, Roles.Funding, Roles.Coordinator, Roles.Admin);
    public static bool CanRead(ClaimsPrincipal principal, AssistanceRequest req) => Staff(principal) || req.CitizenId == UserId(principal);
    public static bool Coordinates(double lat, double lon) => lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180;
}
public static class TraceLog
{
    public static void Add(AidDbContext db, string entity, Guid id, string action, Guid? actor, object? details = null) =>
        db.Audits.Add(new AuditEvent { EntityType = entity, EntityId = id, EventType = action, ActorId = actor,
            DetailJson = JsonSerializer.Serialize(details ?? new { }) });
    public static void History(AidDbContext db, Guid req, Guid? actor, string action, string notes) =>
        db.RequestHistories.Add(new RequestHistory { RequestId = req, ActorId = actor, Action = action, Notes = notes });
    public static void Queue(AidDbContext db, Guid recipient, string key, string title, string message, string language = "en")
    {
        // Caller must use a deterministic key for each logical event. Unique DB index prevents duplicates.
        db.Notifications.Add(new Notification { RecipientId = recipient, Title = title, Message = message, Language = language });
        db.Outbox.Add(new OutboxMessage { RecipientId = recipient, EventKey = key, Subject = title, Body = message });
    }
}
public static class Distances
{
    public static double Km(double a, double b, double c, double d)
    {
        const double r = 6371;
        double rad = Math.PI / 180.0;
        double deltaLat = (c - a) * rad, deltaLon = (d - b) * rad;
        double x = Math.Pow(Math.Sin(deltaLat / 2), 2) + Math.Cos(a * rad) * Math.Cos(c * rad) * Math.Pow(Math.Sin(deltaLon / 2), 2);
        return 2 * r * Math.Asin(Math.Min(1, Math.Sqrt(x)));
    }
}
public static class MatchEngine
{
    public static IEnumerable<object> ResourcesFor(AssistanceRequest req, IEnumerable<ResourceItem> resources) =>
        resources.Where(r => r.IsActive && r.Category.Equals(req.Category, StringComparison.OrdinalIgnoreCase) &&
            r.AvailableQuantity > 0 && (!req.AccessibilityNeeded || r.Accessible) &&
            (r.ExpiresAt == null || r.ExpiresAt > DateTime.UtcNow) &&
            !r.Condition.Equals("Damaged", StringComparison.OrdinalIgnoreCase) &&
            !r.Condition.Equals("Expired", StringComparison.OrdinalIgnoreCase))
        .Select(r => {
            double distanceKm = Math.Round(Distances.Km(req.Latitude, req.Longitude, r.Latitude, r.Longitude), 1);
            bool isFull = r.AvailableQuantity >= req.RequestedQuantity;
            int allocatable = Math.Min(r.AvailableQuantity, req.RequestedQuantity);
            int unmet = Math.Max(0, req.RequestedQuantity - r.AvailableQuantity);
            
            // Condition score bonus
            int conditionScore = r.Condition.ToLowerInvariant() switch {
                "good" or "new" or "excellent" => 15,
                "fair" => 5,
                _ => 0
            };

            // Expiry buffer score
            int expiryScore = 0;
            if (r.ExpiresAt.HasValue)
            {
                var daysLeft = (r.ExpiresAt.Value - DateTime.UtcNow).TotalDays;
                if (daysLeft > 30) expiryScore = 10;
                else if (daysLeft > 7) expiryScore = 5;
            }
            else expiryScore = 10; // non-perishable

            double score = (isFull ? 45 : 15) +
                (req.AccessibilityNeeded && r.Accessible ? 15 : 0) +
                conditionScore + expiryScore +
                Math.Max(0, 30 - distanceKm);

            var reasons = new List<string>();
            if (isFull) reasons.Add($"Full demand coverable ({allocatable} of {req.RequestedQuantity} {r.Unit})");
            else reasons.Add($"Partial coverable ({allocatable} of {req.RequestedQuantity} {r.Unit}, unmet: {unmet})");
            reasons.Add($"Proximity {distanceKm} km");
            if (r.Accessible && req.AccessibilityNeeded) reasons.Add("Accessible verified");
            reasons.Add($"Condition: {r.Condition}");
            if (r.ExpiresAt.HasValue) reasons.Add($"Valid until {r.ExpiresAt.Value:yyyy-MM-dd}");

            string rationale = string.Join("; ", reasons) + ".";

            return new {
                r.Id,
                r.Name,
                r.Category,
                r.AvailableQuantity,
                r.TotalQuantity,
                r.ReservedQuantity,
                r.Unit,
                r.Area,
                r.Condition,
                r.BatchNumber,
                DistanceKm = distanceKm,
                AvailableForFullRequest = isFull,
                AllocatableQuantity = allocatable,
                RemainingUnmetDemand = unmet,
                Score = Math.Round(score, 1),
                Rationale = rationale
            };
        })
        .OrderByDescending(r => r.Score).Cast<object>();
}
