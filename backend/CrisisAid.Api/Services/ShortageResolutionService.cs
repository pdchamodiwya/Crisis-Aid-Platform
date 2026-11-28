using CrisisAid.Api.Contracts;
using CrisisAid.Api.Data;
using CrisisAid.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrisisAid.Api.Services;

public interface IShortageResolutionService
{
    Task<object> AnalyzeShortagesAsync(string? category, Guid? crisisId, CancellationToken ct = default);
    Task<object> GenerateSuggestedPlanAsync(ShortagePlanRequestDto dto, CancellationToken ct = default);
    Task<object> ApplyShortagePlanAsync(ResolveShortagePlanDto dto, Guid actorId, CancellationToken ct = default);
}

public class ShortageResolutionService(AidDbContext db) : IShortageResolutionService
{
    public async Task<object> AnalyzeShortagesAsync(string? category, Guid? crisisId, CancellationToken ct = default)
    {
        IQueryable<AssistanceRequest> reqQuery = db.Requests.AsNoTracking()
            .Where(r => r.Status == "Verified" || r.Status == "Escalated");

        if (!string.IsNullOrWhiteSpace(category) && category != "All")
            reqQuery = reqQuery.Where(r => r.Category == category);

        if (crisisId.HasValue)
            reqQuery = reqQuery.Where(r => r.CrisisId == crisisId.Value);

        var pendingRequests = await reqQuery.ToListAsync(ct);
        var resources = await db.Resources.AsNoTracking().Where(r => r.IsActive).ToListAsync(ct);

        var analysis = pendingRequests.GroupBy(r => r.Category).Select(g =>
        {
            var categoryStock = resources.Where(r => r.Category.Equals(g.Key, StringComparison.OrdinalIgnoreCase)).ToList();
            int totalAvailable = categoryStock.Sum(r => r.AvailableQuantity);
            int totalRequired = g.Sum(r => r.RequestedQuantity);
            int shortage = Math.Max(0, totalRequired - totalAvailable);

            var competing = g.Select(r => new
            {
                r.Id,
                r.Category,
                r.Urgency,
                r.PeopleAffected,
                r.RequestedQuantity,
                r.MedicalEmergency,
                r.AccessibilityNeeded,
                r.CreatedAt,
                PriorityScore = CalculatePriorityWeight(r)
            }).OrderByDescending(x => x.PriorityScore).ThenBy(x => x.CreatedAt).ToList();

            return new
            {
                Category = g.Key,
                PendingRequestsCount = g.Count(),
                TotalRequiredUnits = totalRequired,
                TotalAvailableUnits = totalAvailable,
                ShortageUnits = shortage,
                HasShortage = shortage > 0,
                StockWarehouses = categoryStock.Select(s => new { s.Id, s.Name, s.Area, s.AvailableQuantity, s.Unit }).ToList(),
                CompetingRequests = competing
            };
        }).OrderByDescending(x => x.ShortageUnits).ToList();

        return new
        {
            TotalCategoriesWithShortage = analysis.Count(x => x.HasShortage),
            TotalPendingCases = pendingRequests.Count,
            Categories = analysis
        };
    }

    public async Task<object> GenerateSuggestedPlanAsync(ShortagePlanRequestDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Category))
            throw new ArgumentException("Category is required for shortage plan generation");

        IQueryable<AssistanceRequest> reqQuery = db.Requests.AsNoTracking()
            .Where(r => (r.Status == "Verified" || r.Status == "Escalated") && r.Category.ToLower() == dto.Category.ToLower());

        if (dto.CrisisId.HasValue)
            reqQuery = reqQuery.Where(r => r.CrisisId == dto.CrisisId.Value);

        var requests = await reqQuery.ToListAsync(ct);
        var stock = await db.Resources.AsNoTracking()
            .Where(r => r.IsActive && r.Category.ToLower() == dto.Category.ToLower() && r.AvailableQuantity > 0 &&
                        (r.ExpiresAt == null || r.ExpiresAt > DateTime.UtcNow) &&
                        r.Condition != "Damaged" && r.Condition != "Expired")
            .OrderByDescending(r => r.AvailableQuantity)
            .ToListAsync(ct);

        int totalStockAvailable = stock.Sum(s => s.AvailableQuantity);
        int totalDemand = requests.Sum(r => r.RequestedQuantity);

        // Rank competing requests by priority weight (Critical & Medical first, then headcounts)
        var rankedRequests = requests
            .Select(r => new { Request = r, PriorityWeight = CalculatePriorityWeight(r) })
            .OrderByDescending(x => x.PriorityWeight)
            .ThenBy(x => x.Request.CreatedAt)
            .ToList();

        var suggestedAllocations = new List<object>();
        int runningAvailable = totalStockAvailable;
        var remainingStock = stock.ToDictionary(s => s.Id, s => s.AvailableQuantity);

        foreach (var item in rankedRequests)
        {
            var req = item.Request;
            int needed = req.RequestedQuantity;
            int allocatedForThisRequest = 0;

            // Find matching batches closest to request or with sufficient stock
            foreach (var resource in stock)
            {
                if (needed <= 0) break;
                if (!remainingStock.TryGetValue(resource.Id, out int availableInBatch) || availableInBatch <= 0) continue;
                if (req.AccessibilityNeeded && !resource.Accessible) continue;

                int canTake = Math.Min(needed, availableInBatch);
                allocatedForThisRequest += canTake;
                needed -= canTake;
                remainingStock[resource.Id] -= canTake;

                suggestedAllocations.Add(new
                {
                    RequestId = req.Id,
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceArea = resource.Area,
                    QuantityRequested = req.RequestedQuantity,
                    QuantityAllocated = canTake,
                    QuantityUnmet = needed,
                    IsPartial = needed > 0,
                    req.Urgency,
                    item.PriorityWeight,
                    Rationale = $"Priority rank {item.PriorityWeight} pts ({req.Urgency}). Allocated {canTake} of {req.RequestedQuantity} {resource.Unit} from {resource.Name}."
                });
            }
        }

        int totalAllocated = totalStockAvailable - remainingStock.Values.Sum();
        int unmetTotal = Math.Max(0, totalDemand - totalAllocated);

        return new
        {
            dto.Category,
            TotalDemand = totalDemand,
            TotalAvailableStock = totalStockAvailable,
            TotalAllocatable = totalAllocated,
            UnmetShortage = unmetTotal,
            SuggestedPlan = suggestedAllocations,
            OutstandingShortageQueue = rankedRequests
                .Where(r => !suggestedAllocations.Any(s => ((dynamic)s).RequestId == r.Request.Id && ((dynamic)s).QuantityUnmet == 0))
                .Select(r => new
                {
                    r.Request.Id,
                    r.Request.Urgency,
                    r.Request.PeopleAffected,
                    r.Request.RequestedQuantity,
                    r.PriorityWeight,
                    Status = "QueuedForReplenishmentOrPartnerRequest"
                }).ToList()
        };
    }

    public async Task<object> ApplyShortagePlanAsync(ResolveShortagePlanDto dto, Guid actorId, CancellationToken ct = default)
    {
        if (dto.Allocations == null || dto.Allocations.Count == 0)
            throw new ArgumentException("No allocations provided in plan");

        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        var results = new List<ResourceAllocation>();

        foreach (var item in dto.Allocations)
        {
            if (item.QuantityAllocated <= 0) continue;

            var request = await db.Requests.FindAsync([item.RequestId], ct);
            var resource = await db.Resources.FindAsync([item.ResourceId], ct);

            if (request == null || resource == null)
                throw new InvalidOperationException($"Request {item.RequestId} or Resource {item.ResourceId} not found");

            if (resource.AvailableQuantity < item.QuantityAllocated)
                throw new InvalidOperationException($"Insufficient inventory in {resource.Name}: available {resource.AvailableQuantity}, requested {item.QuantityAllocated}");

            // Deduct available, increment reserved
            resource.AvailableQuantity -= item.QuantityAllocated;
            resource.ReservedQuantity += item.QuantityAllocated;
            resource.UpdatedAt = DateTime.UtcNow;

            int previouslyReserved = await db.ResourceAllocations
                .Where(x => x.RequestId == request.Id && x.Status != "Cancelled")
                .SumAsync(x => (int?)x.Quantity, ct) ?? 0;

            int totalAllocated = previouslyReserved + item.QuantityAllocated;
            int unmet = Math.Max(0, request.RequestedQuantity - totalAllocated);

            request.Status = "Assigned";
            request.UpdatedAt = DateTime.UtcNow;

            var allocation = new ResourceAllocation
            {
                RequestId = request.Id,
                ResourceId = resource.Id,
                Quantity = item.QuantityAllocated,
                QuantityRequested = request.RequestedQuantity,
                QuantityAllocated = item.QuantityAllocated,
                QuantityRemaining = unmet,
                Status = "Reserved",
                Notes = $"Plan allocation: {dto.Notes}",
                ApprovedBy = actorId,
                At = DateTime.UtcNow
            };

            db.ResourceAllocations.Add(allocation);

            // Record movement
            db.StockMovements.Add(new StockMovement
            {
                ResourceId = resource.Id,
                MovementType = "Stock Reserved",
                Quantity = item.QuantityAllocated,
                ReferenceId = allocation.Id,
                Notes = $"Reserved under shortage resolution plan for Request #{request.Id.ToString()[..8].ToUpper()}",
                PerformedBy = actorId,
                CreatedAt = DateTime.UtcNow
            });

            TraceLog.History(db, request.Id, actorId, "Shortage Plan Allocation", $"{item.QuantityAllocated} {resource.Unit} reserved ({resource.Name}). Unmet remainder: {unmet}");
            TraceLog.Queue(db, request.CitizenId, $"allocation:{allocation.Id}", "Aid Allocated", $"{item.QuantityAllocated} {resource.Unit} reserved for your request under humanitarian allocation plan.");

            results.Add(allocation);
        }

        TraceLog.Add(db, "ShortagePlan", Guid.NewGuid(), "Applied", actorId, new { AllocationsCount = results.Count, dto.Notes });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new
        {
            Success = true,
            AllocationsCount = results.Count,
            Allocations = results.Select(a => new { a.Id, a.RequestId, a.ResourceId, a.Quantity, a.Status })
        };
    }

    private static int CalculatePriorityWeight(AssistanceRequest r)
    {
        int baseWeight = r.Urgency.ToLowerInvariant() switch
        {
            "critical" => 90,
            "high" => 70,
            "medium" => 40,
            _ => 20
        };

        if (r.MedicalEmergency) baseWeight += 15;
        if (r.AccessibilityNeeded) baseWeight += 10;
        baseWeight += Math.Min(15, r.PeopleAffected * 2);

        return baseWeight;
    }
}
