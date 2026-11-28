namespace CrisisAid.Api.Services.AI;

public record ForecastDemandPoint(
    Guid CrisisId,
    string Area,
    string Category,
    string Unit,
    DateTime CreatedAt,
    int RequestedQuantity
);

public record ForecastStockPoint(
    string Area,
    string Category,
    string Unit,
    int AvailableQuantity
);

public record ForecastResult(
    Guid CrisisId,
    string Area,
    string Category,
    string Unit,
    int HistoricalUnits,
    double PredictedNext7Days,
    int AvailableStock,
    double Shortage,
    int RequestCount,
    bool HasSufficientData,
    string Limitation,
    string Method
);

public interface IAiForecastService
{
    IReadOnlyList<ForecastResult> Forecast(
        IReadOnlyCollection<ForecastDemandPoint> demand,
        IReadOnlyCollection<ForecastStockPoint> stock,
        DateTime asOfUtc);
}

public class AiForecastService : IAiForecastService
{
    private const int HistoryDays = 14;
    private const int ForecastDays = 7;
    private const int MinimumRequestCount = 7;

    public IReadOnlyList<ForecastResult> Forecast(
        IReadOnlyCollection<ForecastDemandPoint> demand,
        IReadOnlyCollection<ForecastStockPoint> stock,
        DateTime asOfUtc)
    {
        var firstDay = asOfUtc.Date.AddDays(-(HistoryDays - 1));

        var validDemand = demand
            .Where(x => x.CreatedAt >= firstDay &&
                        x.CreatedAt <= asOfUtc &&
                        x.RequestedQuantity > 0)
            .ToList();

        var demandGroups = validDemand.GroupBy(x => new
        {
            x.CrisisId,
            Area = Normalize(x.Area),
            Category = Normalize(x.Category),
            Unit = Normalize(x.Unit)
        });

        var results = new List<ForecastResult>();

        foreach (var group in demandGroups)
        {
            var requests = group.ToList();

            var dailyTotals = Enumerable
                .Range(0, HistoryDays)
                .Select(day =>
                {
                    var date = firstDay.AddDays(day);

                    return requests
                        .Where(x => x.CreatedAt.Date == date)
                        .Sum(x => x.RequestedQuantity);
                })
                .ToList();

            var historicalUnits = requests.Sum(x => x.RequestedQuantity);

            var averageDailyDemand =
                dailyTotals.Average();

            var predicted = Math.Ceiling(
                averageDailyDemand * ForecastDays);

            var matchingStock = stock
                .Where(x =>
                    Normalize(x.Area) == group.Key.Area &&
                    Normalize(x.Category) == group.Key.Category &&
                    Normalize(x.Unit) == group.Key.Unit)
                .Sum(x => x.AvailableQuantity);

            var sufficientData =
                requests.Count >= MinimumRequestCount;

            results.Add(new ForecastResult(
                group.Key.CrisisId,
                group.Key.Area,
                group.Key.Category,
                group.Key.Unit,
                historicalUnits,
                predicted,
                matchingStock,
                Math.Max(0, predicted - matchingStock),
                requests.Count,
                sufficientData,
                (sufficientData
                    ? "Estimate based on 14-day historical demand. It is not a trained predictive model."
                    : "Insufficient historical request records. Treat this estimate with caution.")
                    + " Stock is shared at area level and is not allocated to this crisis. Shortage is indicative only.",
                "14-day daily moving average, 7-day demand estimate"
            ));
        }

        return results
            .OrderByDescending(x => x.Shortage)
            .ToList();
    }

    private static string Normalize(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }
}
