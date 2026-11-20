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
