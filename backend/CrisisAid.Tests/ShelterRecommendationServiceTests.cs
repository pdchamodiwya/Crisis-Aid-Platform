using CrisisAid.Api.Domain;
using CrisisAid.Api.Services;
using System.Text.Json;
using Xunit;

namespace CrisisAid.Tests;

public class ShelterRecommendationServiceTests
{
    [Fact]
    public void RankExcludesClosedFullAndInaccessibleSheltersAndReturnsCapacityDetails()
    {
        var request = new AssistanceRequest
        {
            PeopleAffected = 4,
            AccessibilityNeeded = true,
            Latitude = 6.9,
            Longitude = 79.9
        };
        var suitable = new Shelter
        {
            Id = Guid.NewGuid(), Name = "Accessible Centre", Area = "Colombo",
            Status = "Open", Capacity = 12, Occupancy = 2, Accessible = true,
            Latitude = 6.9, Longitude = 79.9
        };
        var shelters = new[]
        {
            suitable,
            new Shelter { Id = Guid.NewGuid(), Name = "Closed", Status = "Closed", Capacity = 20, Accessible = true },
            new Shelter { Id = Guid.NewGuid(), Name = "Full", Status = "Open", Capacity = 4, Occupancy = 4, Accessible = true },
            new Shelter { Id = Guid.NewGuid(), Name = "Inaccessible", Status = "Open", Capacity = 20, Accessible = false }
        };

        var recommendations = new ShelterRecommendationService().Rank(
            request, shelters, new Dictionary<Guid, int> { [suitable.Id] = 2 }, false, false, false);

        var recommendation = Assert.Single(recommendations);
        Assert.Equal(suitable.Id, recommendation.ShelterId);
        Assert.Equal(8, recommendation.FreeSpaces);
        Assert.Equal(2, recommendation.Occupied);
        Assert.Equal(12, recommendation.TotalBeds);
        Assert.Equal(0, recommendation.DistanceKm);
        Assert.InRange(recommendation.MatchScore, 0, 100);
        var json = JsonSerializer.SerializeToElement(recommendation, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(suitable.Id, json.GetProperty("shelterId").GetGuid());
        Assert.Equal(8, json.GetProperty("freeSpaces").GetInt32());
        Assert.Equal(2, json.GetProperty("occupied").GetInt32());
        Assert.Equal(12, json.GetProperty("totalBeds").GetInt32());
    }

    [Fact]
    public void RankPrefersNearerShelterWhenOtherFactorsAreEqual()
    {
        var request = new AssistanceRequest { PeopleAffected = 2, Latitude = 6.9, Longitude = 79.9 };
        var near = new Shelter
        {
            Id = Guid.NewGuid(), Name = "Near", Status = "Open", Capacity = 10,
            Accessible = true, Latitude = 6.9, Longitude = 79.9
        };
        var far = new Shelter
        {
            Id = Guid.NewGuid(), Name = "Far", Status = "Open", Capacity = 10,
            Accessible = true, Latitude = 7.9, Longitude = 79.9
        };

        var recommendations = new ShelterRecommendationService().Rank(
            request, new[] { far, near }, new Dictionary<Guid, int>(), false, false, false);

        Assert.Equal(near.Id, recommendations[0].ShelterId);
        Assert.True(recommendations[0].MatchScore > recommendations[1].MatchScore);
    }

    [Fact]
    public void RankReturnsEmptyWhenNoShelterCanFitTheHousehold()
    {
        var request = new AssistanceRequest { PeopleAffected = 5 };
        var fullShelter = new Shelter
        {
            Id = Guid.NewGuid(), Name = "Full", Status = "Open", Capacity = 5,
            Occupancy = 5, Accessible = true
        };

        var recommendations = new ShelterRecommendationService().Rank(
            request, new[] { fullShelter }, new Dictionary<Guid, int>(), false, false, false);

        Assert.Empty(recommendations);
    }
}
