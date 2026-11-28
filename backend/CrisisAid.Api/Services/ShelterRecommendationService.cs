using CrisisAid.Api.Domain;

namespace CrisisAid.Api.Services;

public interface IShelterRecommendationService
{
    IReadOnlyList<ShelterRecommendationResponse> Rank(
        AssistanceRequest request,
        IEnumerable<Shelter> shelters,
        IReadOnlyDictionary<Guid, int> reservedSpaces,
        bool requireMedical,
        bool requireSanitation,
        bool requireFood);
}

public sealed class ShelterRecommendationService : IShelterRecommendationService
{
    public const string StrategyDescription =
        "Suitable shelters are ranked by capacity fit (15-30), accessibility (10-20), distance (0-25), " +
        "medical facilities (0-10), sanitation (0-10), and food (0-5). Distance is Haversine straight-line distance, not road travel.";

    public IReadOnlyList<ShelterRecommendationResponse> Rank(
        AssistanceRequest request,
        IEnumerable<Shelter> shelters,
        IReadOnlyDictionary<Guid, int> reservedSpaces,
        bool requireMedical,
        bool requireSanitation,
        bool requireFood) =>
        shelters
            .Select(shelter => RankOne(request, shelter, reservedSpaces.GetValueOrDefault(shelter.Id),
                requireMedical, requireSanitation, requireFood))
            .Where(recommendation => recommendation.IsSuitable)
            .OrderByDescending(recommendation => recommendation.MatchScore)
            .ToArray();

    private static ShelterRecommendationResponse RankOne(
        AssistanceRequest request,
        Shelter shelter,
        int reserved,
        bool requireMedical,
        bool requireSanitation,
        bool requireFood)
    {
        var freeSpaces = ShelterCapacity.Remaining(shelter.Capacity, shelter.Occupancy, reserved);
        var distanceKm = Distances.Km(request.Latitude, request.Longitude, shelter.Latitude, shelter.Longitude);
        var suitable = ShelterCapacity.IsSuitable(request, shelter, reserved,
            requireMedical, requireSanitation, requireFood);
        var householdSize = Math.Max(1, request.PeopleAffected);
        var capacityRatio = freeSpaces / (double)householdSize;
        var capacityScore = 15 + Math.Clamp((capacityRatio - 1) / 2, 0, 1) * 15;
        var accessibilityScore = request.AccessibilityNeeded || shelter.Accessible ? 20 : 10;
        var distanceScore = Math.Max(0, 25 - distanceKm * 1.25);
        var score = (int)Math.Round(capacityScore + accessibilityScore + distanceScore +
            (shelter.MedicalFacilities ? 10 : 0) +
            (shelter.SanitationFacilities ? 10 : 0) +
            (shelter.FoodAvailable ? 5 : 0));
        var reasons = new List<string>
        {
            $"{freeSpaces} spaces available for {request.PeopleAffected} people",
            request.AccessibilityNeeded ? "Accessibility requirement met" :
                shelter.Accessible ? "Accessible" : "Accessibility not confirmed",
            requireMedical ? "Medical facilities requirement met" :
                shelter.MedicalFacilities ? "Medical facilities available" : "No medical facilities listed",
            requireSanitation ? "Sanitation requirement met" :
                shelter.SanitationFacilities ? "Sanitation available" : "Sanitation not confirmed",
            requireFood ? "Food availability requirement met" :
                shelter.FoodAvailable ? "Food available" : "Food not confirmed",
            $"{distanceKm:0.0} km straight-line distance"
        };

        return new ShelterRecommendationResponse(shelter.Id, shelter.Name, shelter.Area, score, freeSpaces,
            shelter.Capacity, shelter.Occupancy, reserved, Math.Round(distanceKm, 1), reasons, suitable);
    }
}
