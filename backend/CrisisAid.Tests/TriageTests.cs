using CrisisAid.Api.Domain;
using CrisisAid.Api.Services;
using Xunit;
namespace CrisisAid.Tests;

public class TriageTests
{
    [Fact] public void CriticalMedicalRequestScoresHigherThanLowPriorityFood()
    {
        var urgent = new AssistanceRequest { Category = "Medicine", Urgency = "Critical", MedicalEmergency = true };
        var routine = new AssistanceRequest { Category = "Food", Urgency = "Low" };
        Assert.True(AiDecisionService.Fallback(urgent).Priority > AiDecisionService.Fallback(routine).Priority);
    }
    [Fact] public void PriorityIsClampedTo100() => Assert.InRange(AiDecisionService.Fallback(
        new AssistanceRequest { Urgency = "Critical", MedicalEmergency = true, PeopleAffected = 50, AccessibilityNeeded = true }).Priority, 1, 100);
    [Fact] public void CategoryCanBeInferredFromText() => Assert.Equal("Water", AiDecisionService.Fallback(
        new AssistanceRequest { Category = "Other", Description = "Need clean drinking water" }).Category);
    [Fact] public void MatchingExcludesInsufficientAccessibilityAndExpiredStock()
    {
        var req = new AssistanceRequest { Category = "Water", AccessibilityNeeded = true, RequestedQuantity = 5, Latitude = 6.9, Longitude = 79.9 };
        var resources = new[] {
            new ResourceItem { Category = "Water", Name = "Suitable", AvailableQuantity = 10, Accessible = true },
            new ResourceItem { Category = "Water", Name = "Inaccessible", AvailableQuantity = 10, Accessible = false },
            new ResourceItem { Category = "Water", Name = "Expired", AvailableQuantity = 10, Accessible = true, ExpiresAt = DateTime.UtcNow.AddDays(-1) } };
        Assert.Single(MatchEngine.ResourcesFor(req, resources));
    }
    [Fact] public void DistanceIdenticalPointsIsZero() => Assert.Equal(0, Distances.Km(6.9, 79.9, 6.9, 79.9), 5);
}

public class MatchingPolicyTests
{
    [Fact] public void FullOnlyMatchingExcludesPartialStock()
    {
        var r = new AssistanceRequest { Category = "Food", RequestedQuantity = 10 };
        var stock = new[] { new ResourceItem { Category = "Food", AvailableQuantity = 4 } };
        var factory = new ResourceMatchStrategyFactory();
        Assert.Empty(factory.Create(false).Match(r, stock));
        Assert.Single(factory.Create(true).Match(r, stock));
    }
}

public class ShelterCapacityTests
{
    [Fact]
    public void ReservationsReduceAvailableBedsWithoutChangingPhysicalOccupancy()
    {
        Assert.Equal(15, ShelterCapacity.Remaining(100, 60, 25));
        Assert.True(ShelterCapacity.CanReserve(100, 60, 25, 15));
        Assert.False(ShelterCapacity.CanReserve(100, 60, 25, 16));
    }

    [Fact]
    public void ShelterRecommendationsExcludeAccessibilityAndFacilityMismatches()
    {
        var request = new AssistanceRequest { PeopleAffected = 4, AccessibilityNeeded = true, MedicalEmergency = true };
        var inaccessible = new Shelter
        {
            Capacity = 10, Accessible = false, MedicalFacilities = true,
            SanitationFacilities = true, Status = "Open"
        };
        var accessibleWithoutMedical = new Shelter
        {
            Capacity = 10, Accessible = true, MedicalFacilities = false,
            SanitationFacilities = true, Status = "Open"
        };

        Assert.False(ShelterCapacity.IsSuitable(request, inaccessible, 0, true, false, false));
        Assert.False(ShelterCapacity.IsSuitable(request, accessibleWithoutMedical, 0, true, false, false));
    }
}

public class VolunteerScheduleTests
{
    [Fact]
    public void OverlappingActiveAssignmentsAreRejectedButAdjacentTasksAreAllowed()
    {
        var start = new DateTime(2026, 11, 28, 10, 0, 0, DateTimeKind.Utc);
        var assignment = new VolunteerTask
        {
            ScheduledStartAt = start,
            DurationMinutes = 60,
            Status = "Accepted"
        };

        Assert.True(VolunteerSchedule.Overlaps(start.AddMinutes(30), 30, new[] { assignment }));
        Assert.False(VolunteerSchedule.Overlaps(start.AddHours(1), 30, new[] { assignment }));
        Assert.False(VolunteerSchedule.Overlaps(start.AddMinutes(30), 30,
            new[] { new VolunteerTask { ScheduledStartAt = start, DurationMinutes = 60, Status = "Cancelled" } }));
    }
}

public class VolunteerSkillTests
{
    [Fact]
    public void StructuredButUnverifiedSkillsDoNotUseLegacyTextAsVerification()
    {
        var profile = new VolunteerProfile { Skills = "First aid" };
        var skills = new[] { new VolunteerSkill { Name = "First aid", Verified = false } };

        Assert.False(VolunteerSkillMatcher.HasVerifiedSkill(profile, skills, "First aid"));
    }

    [Fact]
    public void LegacyProfilesRemainMatchableUntilStructuredSkillsAreAdded()
    {
        var profile = new VolunteerProfile { Skills = "First aid, translation" };

        Assert.True(VolunteerSkillMatcher.HasVerifiedSkill(profile, Array.Empty<VolunteerSkill>(), "translation"));
    }
}
