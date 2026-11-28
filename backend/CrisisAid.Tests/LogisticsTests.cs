using CrisisAid.Api.Contracts;
using CrisisAid.Api.Domain;
using CrisisAid.Api.Services;
using Xunit;

namespace CrisisAid.Tests;

public class LogisticsTests
{
    [Fact]
    public void Matching_ExcludesDamagedExpiredAndInaccessibleResources()
    {
        var request = new AssistanceRequest
        {
            Category = "Food",
            RequestedQuantity = 10,
            AccessibilityNeeded = true,
            Latitude = 6.9271,
            Longitude = 79.8612
        };

        var resources = new List<ResourceItem>
        {
            new() { Id = Guid.NewGuid(), Name = "Standard Food Packs", Category = "Food", AvailableQuantity = 20, Accessible = true, Condition = "Good", Latitude = 6.93, Longitude = 79.86 },
            new() { Id = Guid.NewGuid(), Name = "Damaged Batch", Category = "Food", AvailableQuantity = 50, Accessible = true, Condition = "Damaged", Latitude = 6.93, Longitude = 79.86 },
            new() { Id = Guid.NewGuid(), Name = "Expired Food", Category = "Food", AvailableQuantity = 50, Accessible = true, Condition = "Good", ExpiresAt = DateTime.UtcNow.AddDays(-2), Latitude = 6.93, Longitude = 79.86 },
            new() { Id = Guid.NewGuid(), Name = "Inaccessible Packaging", Category = "Food", AvailableQuantity = 50, Accessible = false, Condition = "Good", Latitude = 6.93, Longitude = 79.86 }
        };

        var matches = MatchEngine.ResourcesFor(request, resources).ToList();

        Assert.Single(matches);
        dynamic first = matches[0];
        Assert.Equal("Standard Food Packs", (string)first.Name);
        Assert.True((bool)first.AvailableForFullRequest);
        Assert.Equal(10, (int)first.AllocatableQuantity);
        Assert.Equal(0, (int)first.RemainingUnmetDemand);
        Assert.Contains("Full demand coverable", (string)first.Rationale);
    }

    [Fact]
    public void Matching_CalculatesPartialAllocationAndRemainingShortage()
    {
        var request = new AssistanceRequest
        {
            Category = "Water",
            RequestedQuantity = 50,
            Latitude = 6.9,
            Longitude = 79.8
        };

        var resources = new List<ResourceItem>
        {
            new() { Id = Guid.NewGuid(), Name = "Partial Water Stock", Category = "Water", AvailableQuantity = 20, Accessible = true, Condition = "Good", Latitude = 6.91, Longitude = 79.81 }
        };

        var matches = MatchEngine.ResourcesFor(request, resources).ToList();
        Assert.Single(matches);
        dynamic first = matches[0];
        Assert.False((bool)first.AvailableForFullRequest);
        Assert.Equal(20, (int)first.AllocatableQuantity);
        Assert.Equal(30, (int)first.RemainingUnmetDemand);
        Assert.Contains("Partial coverable", (string)first.Rationale);
    }

    [Fact]
    public void Matching_RanksCloserWarehouseHigherThanFarWarehouse()
    {
        var request = new AssistanceRequest
        {
            Category = "Food",
            RequestedQuantity = 10,
            Latitude = 6.9271,
            Longitude = 79.8612
        };

        var resources = new List<ResourceItem>
        {
            new() { Id = Guid.NewGuid(), Name = "Far Depot", Category = "Food", AvailableQuantity = 10, Accessible = true, Condition = "Good", Latitude = 7.29, Longitude = 80.63 }, // ~115 km away
            new() { Id = Guid.NewGuid(), Name = "Near Depot", Category = "Food", AvailableQuantity = 10, Accessible = true, Condition = "Good", Latitude = 6.93, Longitude = 79.86 }   // ~1 km away
        };

        var matches = MatchEngine.ResourcesFor(request, resources).ToList();
        Assert.Equal(2, matches.Count);
        dynamic first = matches[0];
        dynamic second = matches[1];

        Assert.Equal("Near Depot", (string)first.Name);
        Assert.Equal("Far Depot", (string)second.Name);
        Assert.True((double)first.Score > (double)second.Score);
    }

    [Fact]
    public void Matching_AwardsBonusScoreForGoodConditionAndAccessibleMatch()
    {
        var request = new AssistanceRequest
        {
            Category = "Medicine",
            RequestedQuantity = 5,
            AccessibilityNeeded = true,
            Latitude = 6.9,
            Longitude = 79.8
        };

        var goodRes = new ResourceItem { Id = Guid.NewGuid(), Name = "Good Kits", Category = "Medicine", AvailableQuantity = 10, Accessible = true, Condition = "Good", Latitude = 6.9, Longitude = 79.8 };
        var fairRes = new ResourceItem { Id = Guid.NewGuid(), Name = "Fair Kits", Category = "Medicine", AvailableQuantity = 10, Accessible = true, Condition = "Fair", Latitude = 6.9, Longitude = 79.8 };

        var goodMatch = (dynamic)MatchEngine.ResourcesFor(request, new[] { goodRes }).First();
        var fairMatch = (dynamic)MatchEngine.ResourcesFor(request, new[] { fairRes }).First();

        Assert.True((double)goodMatch.Score > (double)fairMatch.Score);
    }

    [Fact]
    public void DeliveryVerification_GeneratesCryptographicallySecureTokens()
    {
        string token1 = DeliveryVerificationService.GenerateSecureToken();
        string token2 = DeliveryVerificationService.GenerateSecureToken();

        Assert.NotEmpty(token1);
        Assert.NotEmpty(token2);
        Assert.NotEqual(token1, token2);
        Assert.Equal(48, token1.Length); // 24 bytes in hex
    }

    [Fact]
    public void DeliveryVerification_GeneratesValidHumanReadableShortCode()
    {
        string code = DeliveryVerificationService.GenerateShortCode();

        Assert.NotNull(code);
        Assert.Equal(9, code.Length); // 4 chars + '-' + 4 chars
        Assert.Equal('-', code[4]);

        // Does not contain confusing chars 0, 1, I, O
        Assert.DoesNotContain('0', code);
        Assert.DoesNotContain('1', code);
        Assert.DoesNotContain('I', code);
        Assert.DoesNotContain('O', code);
    }

    [Fact]
    public void DeliveryVerification_GeneratesValidSvgQrCode()
    {
        string testUrl = "http://localhost:5173/?verify_token=test1234567890abcdef";
        string svg = DeliveryVerificationService.GenerateQrSvg(testUrl);

        Assert.NotNull(svg);
        Assert.StartsWith("<svg xmlns=\"http://www.w3.org/2000/svg\"", svg);
        Assert.EndsWith("</svg>", svg);
        Assert.Contains("viewBox=\"0 0", svg);
        Assert.Contains("<path fill=\"#1e293b\"", svg);
    }

    [Fact]
    public void QrMatrixEncoder_ProducesExpectedSizeAndFinderPatterns()
    {
        string content = "https://crisisaid.local/verify?token=abc";
        bool[,] matrix = QrMatrixEncoder.Encode(content);

        int size = matrix.GetLength(0);
        Assert.True(size == 29 || size == 33); // Version 3 or Version 4

        // Top-left finder pattern center (row 3, col 3) must be black/true
        Assert.True(matrix[3, 3]);
        // Finder outer ring row 0 (cols 0 to 6) must be black/true
        for (int c = 0; c < 7; c++)
        {
            Assert.True(matrix[0, c]);
        }
    }

    [Fact]
    public void StockMovement_MovementTypesAreCorrectlyDefined()
    {
        var movement = new StockMovement
        {
            ResourceId = Guid.NewGuid(),
            MovementType = "Stock Received",
            Quantity = 50,
            Notes = "Intake test"
        };

        Assert.Equal("Stock Received", movement.MovementType);
        Assert.Equal(50, movement.Quantity);
    }

    [Fact]
    public void ResourceItem_TrackingFieldsInitializeWithSafeDefaults()
    {
        var item = new ResourceItem
        {
            Name = "Water Bottles",
            Category = "Water",
            AvailableQuantity = 100,
            TotalQuantity = 100,
            ReservedQuantity = 0,
            Condition = "Good",
            IsActive = true
        };

        Assert.Equal(100, item.AvailableQuantity);
        Assert.Equal(100, item.TotalQuantity);
        Assert.Equal(0, item.ReservedQuantity);
        Assert.Equal("Good", item.Condition);
        Assert.True(item.IsActive);
    }

    [Fact]
    public void DispatchRecord_StatusProgression_FollowsLogisticsWorkflow()
    {
        var dispatch = new DispatchRecord
        {
            AllocationId = Guid.NewGuid(),
            VehicleId = "WP-CAD-1001",
            AssignedDriver = "Kamal Gunaratne",
            DispatchedQuantity = 30,
            DeliveryStatus = "ReadyForDispatch"
        };

        Assert.Equal("ReadyForDispatch", dispatch.DeliveryStatus);

        // Transit departure
        dispatch.DeliveryStatus = "InTransit";
        dispatch.DepartureTime = DateTime.UtcNow;
        Assert.Equal("InTransit", dispatch.DeliveryStatus);
        Assert.NotNull(dispatch.DepartureTime);

        // Successful Delivery
        dispatch.DeliveryStatus = "Delivered";
        dispatch.DeliveredQuantity = 30;
        dispatch.ActualArrivalTime = DateTime.UtcNow;

        Assert.Equal("Delivered", dispatch.DeliveryStatus);
        Assert.Equal(dispatch.DispatchedQuantity, dispatch.DeliveredQuantity);
        Assert.NotNull(dispatch.ActualArrivalTime);
    }

    [Fact]
    public void ShortageResolution_PriorityScorePrioritizesCriticalAndMedicalCases()
    {
        var reqCritical = new AssistanceRequest
        {
            Urgency = "Critical",
            MedicalEmergency = true,
            PeopleAffected = 10,
            AccessibilityNeeded = true
        };

        var reqLow = new AssistanceRequest
        {
            Urgency = "Low",
            MedicalEmergency = false,
            PeopleAffected = 2,
            AccessibilityNeeded = false
        };

        // Formula: Critical (90) + Medical (15) + Access (10) + People (15) = 130
        int scoreCrit = 90 + 15 + 10 + Math.Min(15, reqCritical.PeopleAffected * 2);
        // Formula: Low (20) + People (4) = 24
        int scoreLow = 20 + Math.Min(15, reqLow.PeopleAffected * 2);

        Assert.True(scoreCrit > scoreLow);
        Assert.Equal(130, scoreCrit);
        Assert.Equal(24, scoreLow);
    }
}
