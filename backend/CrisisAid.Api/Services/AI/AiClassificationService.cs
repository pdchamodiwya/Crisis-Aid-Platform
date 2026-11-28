
namespace CrisisAid.Api.Services.AI;

public record ClassificationResult(
    string RecommendedCategory,
    double Confidence,
    string Rationale,
    string Strategy
);

public interface IAiClassificationService
{
    ClassificationResult Classify(string selectedCategory, string description);
}

public class AiClassificationService : IAiClassificationService
{
    private static readonly Dictionary<string, string[]> Keywords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Medicine"] =
            [
                "medicine", "medical", "hospital", "doctor", "injury",
                "injured", "fever", "illness", "tablet", "drug",
                "first aid", "ambulance", "bleeding", "sick"
            ],

            ["Water"] =
            [
                "water", "drinking water", "thirst", "dehydration",
                "dehydrated", "water supply", "clean water"
            ],

            ["Food"] =
            [
                "food", "hungry", "hunger", "meal", "rice",
                "bread", "cooking", "eating"
            ],

            ["Shelter"] =
            [
                "shelter", "homeless", "roof", "house damaged",
                "house destroyed", "evacuation", "displaced",
                "temporary housing"
            ],

            ["Transport"] =
            [
                "transport", "vehicle", "bus", "truck", "ambulance",
                "road access", "travel", "evacuation transport"
            ],

            ["Accessibility"] =
            [
                "wheelchair", "disability", "disabled", "accessible",
                "mobility", "elderly", "special needs",
                "mobility assistance", "ramp"
            ]
        };

    public ClassificationResult Classify(
        string selectedCategory,
        string description)
    {
        description ??= string.Empty;
        selectedCategory ??= "Other";

        var text = description.Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            return new ClassificationResult(
                selectedCategory,
                0.0,
                "Description is empty. The citizen-selected category is retained.",
                "keyword-classification-v1"
            );
        }

        var scores = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var category in Keywords)
        {
            int score = 0;

            foreach (var keyword in category.Value)
            {
                if (text.Contains(
                    keyword,
                    StringComparison.OrdinalIgnoreCase))
                {
                    score++;
                }
            }

            scores[category.Key] = score;
        }

        var best = scores
            .Where(x => x.Value > 0)
            .OrderByDescending(x => x.Value)
            .FirstOrDefault();

        if (best.Value == 0)
        {
            return new ClassificationResult(
                selectedCategory,
                0.40,
                "No strong category keywords were found. The citizen-selected category is retained for human review.",
                "keyword-classification-v1"
            );
        }

        int totalMatches = scores.Values.Sum();

        double confidence = Math.Clamp(
            (double)best.Value / Math.Max(totalMatches, 1),
            0.50,
            0.95
        );

        string rationale =
            $"Detected {best.Value} matching keyword(s) for {best.Key}. " +
            $"Citizen-selected category: {selectedCategory}. " +
            "This is an AI-assisted recommendation, not an automatic case update.";

        return new ClassificationResult(
            best.Key,
            confidence,
            rationale,
            "keyword-classification-v1"
        );
    }
}
