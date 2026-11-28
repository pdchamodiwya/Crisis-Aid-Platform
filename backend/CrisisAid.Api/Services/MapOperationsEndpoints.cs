using System.Text.Json;
using CrisisAid.Api.Data;
using CrisisAid.Api.Domain;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace CrisisAid.Api.Services;

public static class MapOperationsEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/map/reverse-geocode", async (double latitude, double longitude,
            IHttpClientFactory clients, IConfiguration configuration, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            if (!Access.Coordinates(latitude, longitude))
                return Results.BadRequest(new { error = "Invalid coordinates" });
            var baseUrl = configuration["Map:ReverseGeocodingBaseUrl"] ?? "https://nominatim.openstreetmap.org/reverse";
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                return Results.Problem("Reverse-geocoding provider is not configured with a valid HTTPS URL",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            var url = $"{uri}?format=jsonv2&lat={latitude:R}&lon={longitude:R}";
            try
            {
                using var response = await clients.CreateClient("map").GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                    return Results.Problem("Reverse-geocoding provider returned an unavailable response",
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                var json = await response.Content.ReadAsStringAsync(ct);
                return Results.Content(json, "application/json");
            }
            catch (HttpRequestException ex)
            {
                loggerFactory.CreateLogger("MapOperations").LogWarning(ex, "Reverse-geocoding provider unavailable");
                return Results.Problem("Reverse-geocoding provider unavailable; use the coordinates as entered",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                loggerFactory.CreateLogger("MapOperations").LogWarning(ex, "Reverse-geocoding provider timed out");
                return Results.Problem("Reverse-geocoding provider timed out; use the coordinates as entered",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).RequireRateLimiting("geocode")
          .WithSummary("Convert coordinates to a nearby address using the configured geocoder");

        api.MapGet("/map/route", async (double fromLat, double fromLon, double toLat, double toLon,
            IHttpClientFactory clients, IConfiguration configuration, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            if (!Access.Coordinates(fromLat, fromLon) || !Access.Coordinates(toLat, toLon))
                return Results.BadRequest(new { error = "Invalid coordinates" });
            var baseUrl = configuration["Map:RoutingBaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseUrl) && Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttps)
            {
                var url = $"{uri.ToString().TrimEnd('/')}/route/v1/driving/{fromLon:R},{fromLat:R};{toLon:R},{toLat:R}?overview=false";
                try
                {
                    using var response = await clients.CreateClient("map").GetAsync(url, ct);
                    response.EnsureSuccessStatusCode();
                    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                    var root = json.RootElement;
                    if (root.TryGetProperty("code", out var code) && code.GetString() == "Ok" &&
                        root.TryGetProperty("routes", out var routes) && routes.GetArrayLength() > 0)
                    {
                        var route = routes[0];
                        return Results.Ok(new
                        {
                            provider = "OSRM",
                            isFallback = false,
                            distanceKm = Math.Round(route.GetProperty("distance").GetDouble() / 1000, 2),
                            durationMinutes = Math.Round(route.GetProperty("duration").GetDouble() / 60, 1)
                        });
                    }
                    loggerFactory.CreateLogger("MapOperations").LogWarning("Routing provider returned no route; using labelled fallback");
                }
                catch (HttpRequestException ex)
                {
                    loggerFactory.CreateLogger("MapOperations").LogWarning(ex, "Routing provider unavailable; using labelled fallback");
                }
                catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
                {
                    loggerFactory.CreateLogger("MapOperations").LogWarning(ex, "Routing provider timed out; using labelled fallback");
                }
                catch (JsonException ex)
                {
                    loggerFactory.CreateLogger("MapOperations").LogWarning(ex, "Routing provider returned invalid JSON; using labelled fallback");
                }
                catch (InvalidOperationException ex)
                {
                    loggerFactory.CreateLogger("MapOperations").LogWarning(ex, "Routing provider response was incomplete; using labelled fallback");
                }
            }
            var straightLineKm = Math.Round(Distances.Km(fromLat, fromLon, toLat, toLon), 2);
            return Results.Ok(new
            {
                provider = "Haversine straight-line fallback",
                isFallback = true,
                distanceKm = straightLineKm,
                durationMinutes = (double?)null,
                message = "This is not a road route or travel-time estimate."
            });
        }).WithSummary("Calculate road distance when configured, otherwise return a labelled straight-line fallback");

        api.MapGet("/map/service-areas", async (AidDbContext db) =>
            Results.Ok(await db.ServiceAreas.AsNoTracking().OrderBy(x => x.Name)
                .Select(x => new { x.Id, x.Name, x.Latitude, x.Longitude, x.RadiusKm }).ToListAsync()))
          .WithSummary("List configured operational service areas");

        api.MapGet("/map/service-centres", async (AidDbContext db) =>
        {
            var shelters = await db.Shelters.AsNoTracking().Select(x => new
            {
                x.Id, Type = "Shelter", x.Name, x.Area, x.Status, x.Accessible,
                x.Latitude, x.Longitude, x.Facilities, x.Capacity, x.Occupancy
            }).ToListAsync();
            var centres = await db.AssistanceCentres.AsNoTracking().Select(x => new
            {
                x.Id, x.Type, x.Name, x.Area, x.Status, x.Accessible,
                x.Latitude, x.Longitude, x.Facilities, Capacity = (int?)null, Occupancy = (int?)null
            }).ToListAsync();
            return Results.Ok(shelters.Cast<object>().Concat(centres));
        }).WithSummary("List operational shelters and assistance centres");

        api.MapGet("/map/coverage", async (AidDbContext db) =>
        {
            var areas = await db.ServiceAreas.AsNoTracking().ToListAsync();
            var shelters = await db.Shelters.AsNoTracking().Where(x => x.Status == "Open").ToListAsync();
            var centres = await db.AssistanceCentres.AsNoTracking().Where(x => x.Status == "Open").ToListAsync();
            var requests = await db.Requests.AsNoTracking()
                .Where(x => x.Status == "Verified" || x.Status == "Escalated").ToListAsync();
            var reservedByShelter = await db.ShelterAllocations.AsNoTracking().Where(x => x.Status == "Reserved")
                .GroupBy(x => x.ShelterId).Select(g => new { ShelterId = g.Key, Count = g.Sum(x => x.PeopleCount) })
                .ToDictionaryAsync(x => x.ShelterId, x => x.Count);
            var result = areas.Select(area =>
            {
                var nearbyShelters = shelters.Where(x =>
                    Distances.Km(area.Latitude, area.Longitude, x.Latitude, x.Longitude) <= area.RadiusKm).ToArray();
                var nearbyCentres = centres.Where(x =>
                    Distances.Km(area.Latitude, area.Longitude, x.Latitude, x.Longitude) <= area.RadiusKm).ToArray();
                var suitableShelters = nearbyShelters.Count(x =>
                    ShelterCapacity.Remaining(x.Capacity, x.Occupancy, reservedByShelter.GetValueOrDefault(x.Id)) > 0 &&
                    x.Accessible);
                var accessibleCentres = nearbyCentres.Count(x => x.Accessible);
                var coverage = suitableShelters + accessibleCentres > 0;
                var nearbyCases = requests.Count(x =>
                    Distances.Km(area.Latitude, area.Longitude, x.Latitude, x.Longitude) <= area.RadiusKm);
                return new
                {
                    area.Id,
                    area.Name,
                    area.RadiusKm,
                    openShelters = nearbyShelters.Length,
                    suitableAccessibleShelters = suitableShelters,
                    openAssistanceCentres = nearbyCentres.Length,
                    accessibleAssistanceCentres = accessibleCentres,
                    verifiedCasesInArea = nearbyCases,
                    hasNearbyService = coverage,
                    coverage = coverage ? "Covered" : "No nearby service centre",
                    distanceBasis = "Straight-line distance; not a road-route guarantee"
                };
            }).ToArray();
            return Results.Ok(result);
        }).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.CaseManager, Roles.Logistics, Roles.ShelterAdmin, Roles.Admin))
          .WithSummary("Report aggregated service coverage without exposing citizen locations");

        api.MapGet("/map/layers", async (AidDbContext db) =>
        {
            var areas = await db.ServiceAreas.AsNoTracking().ToListAsync();
            var resources = await db.Resources.AsNoTracking().Where(x => x.AvailableQuantity > 0).ToListAsync();
            var requests = await db.Requests.AsNoTracking()
                .Where(x => x.Status == "Verified" || x.Status == "Escalated").ToListAsync();
            var shortages = areas.SelectMany(area =>
            {
                var demand = requests.Where(x => Distances.Km(area.Latitude, area.Longitude,
                    x.Latitude, x.Longitude) <= area.RadiusKm).ToArray();
                return demand.GroupBy(x => x.Category).Select(group =>
                {
                    var resourceQuantity = resources.Where(x => x.Category == group.Key &&
                        Distances.Km(area.Latitude, area.Longitude, x.Latitude, x.Longitude) <= area.RadiusKm)
                        .Sum(x => x.AvailableQuantity);
                    var required = group.Sum(x => x.RequestedQuantity);
                    return new
                    {
                        area.Id,
                        area.Name,
                        area.Latitude,
                        area.Longitude,
                        category = group.Key,
                        verifiedCases = group.Count(),
                        requiredUnits = required,
                        availableUnits = resourceQuantity,
                        shortageUnits = Math.Max(0, required - resourceQuantity)
                    };
                });
            }).Where(x => x.shortageUnits > 0).ToArray();
            return Results.Ok(new
            {
                affectedAreas = areas.Select(x => new { x.Id, x.Name, x.Latitude, x.Longitude, x.RadiusKm }),
                warehouses = resources.Select(x => new
                {
                    x.Id, x.Name, x.Category, x.AvailableQuantity, x.Area, x.Latitude, x.Longitude
                }),
                shortages,
                caseLocations = "Aggregated within service areas; no citizen coordinates are returned."
            });
        }).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.CaseManager, Roles.Logistics,
            Roles.ShelterAdmin, Roles.Admin))
          .WithSummary("Get operational areas, resource locations, and aggregated shortage layers");
    }
}
