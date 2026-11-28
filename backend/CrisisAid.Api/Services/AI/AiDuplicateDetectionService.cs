
using System.Text.RegularExpressions;
using CrisisAid.Api.Domain;

namespace CrisisAid.Api.Services.AI;

public record DuplicateDetectionResult(
    Guid FirstRequestId,
    Guid SecondRequestId,
    double SimilarityScore,
    bool IsPotentialDuplicate,
    string Reason,
    string Strategy
);

public interface IAiDuplicateDetectionService
{
    DuplicateDetectionResult Compare(
        AssistanceRequest first,
        AssistanceRequest second
    );
}

public class AiDuplicateDetectionService : IAiDuplicateDetectionService
{
    private const double SimilarityThreshold = 0.35;
    private const double MaximumDistanceKm = 5.0;
    private const int MaximumTimeDifferenceDays = 14;

    public DuplicateDetectionResult Compare(
        AssistanceRequest first,
        AssistanceRequest second)
    {
        if (first.Id == second.Id)
            return Result(first, second, 0, false,
                "Both records refer to the same request.");

        if (first.CrisisId != second.CrisisId)
            return Result(first, second, 0, false,
                "Requests belong to different crises.");

        var categoryMatches = string.Equals(
            first.Category,
            second.Category,
            StringComparison.OrdinalIgnoreCase
        );

        var daysApart = Math.Abs(
            (first.CreatedAt - second.CreatedAt).TotalDays
        );

        var timeMatches = daysApart <= MaximumTimeDifferenceDays;

        var similarity = CalculateJaccardSimilarity(
            first.Description,
            second.Description
        );

        var distance = CalculateDistanceKm(
            first.Latitude,
            first.Longitude,
            second.Latitude,
            second.Longitude
        );

        var locationMatches = distance <= MaximumDistanceKm;

        double score =
            similarity * 0.60 +
            (categoryMatches ? 0.20 : 0.0) +
            (timeMatches ? 0.10 : 0.0) +
            (locationMatches ? 0.10 : 0.0);

        score = Math.Round(score, 2);

        bool potentialDuplicate =
            categoryMatches &&
            timeMatches &&
            score >= SimilarityThreshold;

        var reasons = new List<string>
        {
            $"Description similarity: {similarity:P0}",
            $"Category match: {categoryMatches}",
            $"Within {MaximumTimeDifferenceDays} days: {timeMatches}",
            $"Geographic distance: {distance:F2} km",
            potentialDuplicate
                ? "Potential duplicate; human review required."
                : "Duplicate threshold not met."
        };

        return Result(
            first,
            second,
            score,
            potentialDuplicate,
            string.Join("; ", reasons)
        );
    }

    private static double CalculateJaccardSimilarity(
        string? first,
        string? second)
    {
        var firstWords = Normalize(first);
        var secondWords = Normalize(second);

        if (firstWords.Count == 0 || secondWords.Count == 0)
            return 0;

        var intersection = firstWords.Intersect(
            secondWords,
            StringComparer.OrdinalIgnoreCase
        ).Count();

        var union = firstWords.Union(
            secondWords,
            StringComparer.OrdinalIgnoreCase
        ).Count();

        return union == 0 ? 0 : (double)intersection / union;
    }

    private static HashSet<string> Normalize(string? description)
    {
        var normalized = (description ?? string.Empty)
            .ToLowerInvariant();

        normalized = Regex.Replace(
            normalized,
            @"[^\p{L}\p{N}\s]",
            " "
        );

        return normalized
            .Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries
            )
            .Where(word => word.Length > 2)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static double CalculateDistanceKm(
        double latitude1,
        double longitude1,
        double latitude2,
        double longitude2)
    {
        const double earthRadiusKm = 6371.0;

        var lat1 = ToRadians(latitude1);
        var lat2 = ToRadians(latitude2);
        var deltaLat = ToRadians(latitude2 - latitude1);
        var deltaLon = ToRadians(longitude2 - longitude1);

        var a =
            Math.Pow(Math.Sin(deltaLat / 2), 2) +
            Math.Cos(lat1) * Math.Cos(lat2) *
            Math.Pow(Math.Sin(deltaLon / 2), 2);

        var c = 2 * Math.Atan2(
            Math.Sqrt(a),
            Math.Sqrt(1 - a)
        );

        return earthRadiusKm * c;
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    private static DuplicateDetectionResult Result(
        AssistanceRequest first,
        AssistanceRequest second,
        double score,
        bool isDuplicate,
        string reason)
    {
        return new DuplicateDetectionResult(
            first.Id,
            second.Id,
            score,
            isDuplicate,
            reason,
            "jaccard-location-time-v1"
        );
    }
}
