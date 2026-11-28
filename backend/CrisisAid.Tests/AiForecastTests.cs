using CrisisAid.Api.Services.AI;
using Xunit;

namespace CrisisAid.Tests;

public class AiForecastTests
{
    private readonly AiForecastService _service = new();

    [Fact]
    public void ForecastCalculatesNextSevenDaysAndShortage()
    {
        var asOf = new DateTime(2026, 11, 28, 12, 0, 0, DateTimeKind.Utc);

        var demand = Enumerable.Range(0, 7)
            .Select(day => new ForecastDemandPoint(
                Guid.NewGuid(),
                "Colombo",
                "Food",
                "packs",
                asOf.AddDays(-7 + day),
                2
            ))
            .ToList();

        var crisisId = demand[0].CrisisId;

        demand = demand
            .Select(x => x with { CrisisId = crisisId })
            .ToList();

        var stock = new[]
        {
            new ForecastStockPoint(
                "Colombo", "Food", "packs", 3)
        };

        var result = Assert.Single(
            _service.Forecast(demand, stock, asOf));

        Assert.Equal(14, result.HistoricalUnits);
        Assert.Equal(7, result.PredictedNext7Days);
        Assert.Equal(3, result.AvailableStock);
        Assert.Equal(4, result.Shortage);
        Assert.True(result.HasSufficientData);
    }

    [Fact]
    public void ForecastWarnsWhenHistoricalDataIsInsufficient()
    {
        var asOf = new DateTime(2026, 11, 28, 12, 0, 0, DateTimeKind.Utc);
        var crisisId = Guid.NewGuid();

        var demand = new[]
        {
            new ForecastDemandPoint(
                crisisId, "Colombo", "Water", "litres",
                asOf.AddDays(-1), 5)
        };

        var result = Assert.Single(
            _service.Forecast(demand, Array.Empty<ForecastStockPoint>(), asOf));

        Assert.False(result.HasSufficientData);
        Assert.Contains("Insufficient", result.Limitation);
        Assert.Equal(3, result.PredictedNext7Days);
    }

    [Fact]
    public void ForecastKeepsDifferentUnitsSeparate()
    {
        var asOf = new DateTime(2026, 11, 28, 12, 0, 0, DateTimeKind.Utc);
        var crisisId = Guid.NewGuid();

        var demand = new[]
        {
            new ForecastDemandPoint(
                crisisId, "Colombo", "Food", "packs",
                asOf.AddDays(-1), 10),

            new ForecastDemandPoint(
                crisisId, "Colombo", "Food", "boxes",
                asOf.AddDays(-1), 20)
        };

        var results = _service.Forecast(
            demand, Array.Empty<ForecastStockPoint>(), asOf);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, x => x.Unit == "packs");
        Assert.Contains(results, x => x.Unit == "boxes");
    }

    [Fact]
    public void ForecastReturnsEmptyWhenThereIsNoDemand()
    {
        var asOf = DateTime.UtcNow;

        var results = _service.Forecast(
            Array.Empty<ForecastDemandPoint>(),
            Array.Empty<ForecastStockPoint>(),
            asOf);

        Assert.Empty(results);
    }
}
