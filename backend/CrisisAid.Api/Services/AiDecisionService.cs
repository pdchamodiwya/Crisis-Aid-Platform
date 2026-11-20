using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CrisisAid.Api.Domain;

namespace CrisisAid.Api.Services;

public record DecisionResult(string Category, int Priority, double Confidence, string Rationale, string Model);

public interface IAiDecisionService { Task<DecisionResult> EvaluateAsync(AssistanceRequest req, CancellationToken ct); }

public class AiDecisionService(IHttpClientFactory clients, IConfiguration config, ILogger<AiDecisionService> logger) : IAiDecisionService
{
    private static readonly string[] Categories = ["Food", "Water", "Medicine", "Shelter", "Transport", "Accessibility", "Other"];
    public static DecisionResult Fallback(AssistanceRequest req)
    {
        var description = req.Description.ToLowerInvariant();
        string category = Categories.FirstOrDefault(c => c.Equals(req.Category, StringComparison.OrdinalIgnoreCase)) ?? "Other";
        if (category == "Other")
        {
            if (new[] { "injur", "fever", "medicine", "hospital", "medical" }.Any(description.Contains)) category = "Medicine";
            else if (new[] { "water", "drinking", "dehydrat" }.Any(description.Contains)) category = "Water";
            else if (new[] { "homeless", "shelter", "roof", "evacuat" }.Any(description.Contains)) category = "Shelter";
            else if (new[] { "hungry", "food", "meal" }.Any(description.Contains)) category = "Food";
            else if (new[] { "transport", "vehicle", "ambulance" }.Any(description.Contains)) category = "Transport";
        }
        int baseScore = req.Urgency.ToLowerInvariant() switch { "critical" => 85, "high" => 68, "low" => 22, _ => 42 };
        int priority = Math.Clamp(baseScore + (req.MedicalEmergency ? 15 : 0) +
            (req.PeopleAffected >= 5 ? 6 : 0) + (req.AccessibilityNeeded ? 5 : 0), 1, 100);
        var factors = $"Urgency={req.Urgency}; medicalEmergency={req.MedicalEmergency}; people={req.PeopleAffected}; accessibility={req.AccessibilityNeeded}. Heuristic screening only; authorised human review required.";
        return new(category, priority, 0.60, factors, "transparent-heuristic-v1");
    }

    public async Task<DecisionResult> EvaluateAsync(AssistanceRequest req, CancellationToken ct)
    {
        var local = Fallback(req);
        var token = config["AI:ApiKey"];
        var model = config["AI:Model"];
        var url = config["AI:Endpoint"];
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(url)) return local;
        try
        {
            // Data minimisation: strip common email/phone identifiers and transmit a short text only.
            // This is NOT guaranteed anonymisation; assess privacy/consent before enabling external AI.
            var shortDescription = req.Description[..Math.Min(req.Description.Length, 300)];
            shortDescription = Regex.Replace(shortDescription, @"[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}", "[REDACTED EMAIL]");
            shortDescription = Regex.Replace(shortDescription, @"\+?\d[\d \-/]{7,}\d", "[REDACTED PHONE]");
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var input = new { category = req.Category, urgency = req.Urgency,
                description = shortDescription,
                peopleAffected = req.PeopleAffected, medicalEmergency = req.MedicalEmergency };
            request.Content = new StringContent(JsonSerializer.Serialize(new {
                model, temperature = 0, response_format = new { type = "json_object" },
                messages = new object[] {
                    new { role = "system", content = "You are a non-autonomous humanitarian triage helper. Return ONLY JSON with category (Food/Water/Medicine/Shelter/Transport/Accessibility/Other), priority integer 1-100, confidence 0-1, rationale short. No operational decisions." },
                    new { role = "user", content = JsonSerializer.Serialize(input) }
                }
            }), Encoding.UTF8, "application/json");
            using var client = clients.CreateClient("ai");
            using var response = await client.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            using var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var message = root.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()!;
            using var parsed = JsonDocument.Parse(message);
            var obj = parsed.RootElement;
            var category = obj.GetProperty("category").GetString() ?? local.Category;
            if (!Categories.Contains(category)) category = local.Category;
            var priority = Math.Clamp(obj.GetProperty("priority").GetInt32(), 1, 100);
            var confidence = Math.Clamp(obj.GetProperty("confidence").GetDouble(), 0.0, 1.0);
            var reason = obj.GetProperty("rationale").GetString() ?? "No rationale returned";
            return new(category, priority, confidence, reason[..Math.Min(600, reason.Length)], $"external:{model}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogWarning("External AI unavailable/invalid; transparent heuristic fallback used. Cause: {Error}", ex.GetType().Name);
            return local with { Rationale = local.Rationale + " External AI unavailable; fallback applied." };
        }
    }
}
