using CrisisAid.Api.Contracts;
using CrisisAid.Api.Data;
using CrisisAid.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrisisAid.Api.Services;

public interface ILogisticsService
{
    Task<List<ResourceItem>> GetInventoryAsync(string? category, string? area, bool? lowStock, bool? expiring, CancellationToken ct = default);
    Task<ResourceItem?> GetResourceByIdAsync(Guid id, CancellationToken ct = default);
    Task<ResourceItem> CreateResourceAsync(CreateResourceDto dto, Guid actorId, CancellationToken ct = default);
    Task<ResourceItem?> UpdateResourceAsync(Guid id, UpdateResourceDto dto, Guid actorId, CancellationToken ct = default);
    Task<bool> DeactivateResourceAsync(Guid id, Guid actorId, CancellationToken ct = default);
    Task<ResourceItem?> ReplenishStockAsync(Guid id, ReplenishResourceDto dto, Guid actorId, CancellationToken ct = default);
    Task<ResourceItem?> AdjustStockAsync(Guid id, AdjustStockDto dto, Guid actorId, CancellationToken ct = default);
    Task<List<StockMovement>> GetMovementsAsync(Guid? resourceId, CancellationToken ct = default);
    Task<List<ResourceItem>> GetLowStockAsync(int threshold = 10, CancellationToken ct = default);
    Task<List<ResourceItem>> GetExpiringAsync(int days = 14, CancellationToken ct = default);
    Task<object> GetLogisticsDashboardMetricsAsync(CancellationToken ct = default);
}

public class LogisticsService(AidDbContext db) : ILogisticsService
{
    public async Task<List<ResourceItem>> GetInventoryAsync(string? category, string? area, bool? lowStock, bool? expiring, CancellationToken ct = default)
    {
        IQueryable<ResourceItem> q = db.Resources.AsNoTracking().Where(r => r.IsActive);

        if (!string.IsNullOrWhiteSpace(category) && category != "All")
            q = q.Where(r => r.Category == category);

        if (!string.IsNullOrWhiteSpace(area) && area != "All")
            q = q.Where(r => r.Area.Contains(area));

        if (lowStock == true)
            q = q.Where(r => r.AvailableQuantity <= 15);

        if (expiring == true)
        {
            var cutoff = DateTime.UtcNow.AddDays(14);
            q = q.Where(r => r.ExpiresAt.HasValue && r.ExpiresAt.Value <= cutoff && r.ExpiresAt.Value >= DateTime.UtcNow);
        }

        return await q.OrderBy(r => r.Category).ThenBy(r => r.Name).ToListAsync(ct);
    }

    public async Task<ResourceItem?> GetResourceByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Resources.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<ResourceItem> CreateResourceAsync(CreateResourceDto dto, Guid actorId, CancellationToken ct = default)
    {
        if (dto.AvailableQuantity < 0)
            throw new ArgumentException("Quantity cannot be negative");

        var item = new ResourceItem
        {
            Name = dto.Name.Trim(),
            Category = dto.Category.Trim(),
            Unit = dto.Unit.Trim(),
            AvailableQuantity = dto.AvailableQuantity,
            TotalQuantity = dto.AvailableQuantity,
            ReservedQuantity = 0,
            Area = dto.Area.Trim(),
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Accessible = dto.Accessible,
            ExpiresAt = dto.ExpiresAt,
            Description = dto.Description.Trim(),
            Condition = string.IsNullOrWhiteSpace(dto.Condition) ? "Good" : dto.Condition.Trim(),
            BatchNumber = dto.BatchNumber?.Trim() ?? $"BATCH-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            OrganisationId = dto.OrganisationId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Resources.Add(item);

        // Record initial Stock Received movement
        db.StockMovements.Add(new StockMovement
        {
            ResourceId = item.Id,
            MovementType = "Stock Received",
            Quantity = item.AvailableQuantity,
            Notes = $"Initial stock intake. Batch: {item.BatchNumber}. Description: {item.Description}",
            PerformedBy = actorId,
            CreatedAt = DateTime.UtcNow
        });

        TraceLog.Add(db, "Resource", item.Id, "Created", actorId, new { item.Name, item.Category, item.AvailableQuantity });
        await db.SaveChangesAsync(ct);
        return item;
    }

    public async Task<ResourceItem?> UpdateResourceAsync(Guid id, UpdateResourceDto dto, Guid actorId, CancellationToken ct = default)
    {
        var item = await db.Resources.FindAsync([id], ct);
        if (item == null) return null;

        item.Name = dto.Name.Trim();
        item.Category = dto.Category.Trim();
        item.Unit = dto.Unit.Trim();
        item.Area = dto.Area.Trim();
        item.Latitude = dto.Latitude;
        item.Longitude = dto.Longitude;
        item.Accessible = dto.Accessible;
        item.ExpiresAt = dto.ExpiresAt;
        item.Description = dto.Description?.Trim() ?? "";
        item.Condition = string.IsNullOrWhiteSpace(dto.Condition) ? "Good" : dto.Condition.Trim();
        item.BatchNumber = dto.BatchNumber?.Trim() ?? item.BatchNumber;
        item.IsActive = dto.IsActive;
        item.UpdatedAt = DateTime.UtcNow;

        TraceLog.Add(db, "Resource", item.Id, "Updated", actorId, new { item.Name, item.Category });
        await db.SaveChangesAsync(ct);
        return item;
    }

    public async Task<bool> DeactivateResourceAsync(Guid id, Guid actorId, CancellationToken ct = default)
    {
        var item = await db.Resources.FindAsync([id], ct);
        if (item == null) return false;

        // Cannot deactivate if actively reserved
        if (item.ReservedQuantity > 0)
            throw new InvalidOperationException($"Cannot deactivate resource with {item.ReservedQuantity} reserved units. Release or complete allocations first.");

        item.IsActive = false;
        item.UpdatedAt = DateTime.UtcNow;

        TraceLog.Add(db, "Resource", item.Id, "Deactivated", actorId);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ResourceItem?> ReplenishStockAsync(Guid id, ReplenishResourceDto dto, Guid actorId, CancellationToken ct = default)
    {
        if (dto.Quantity <= 0)
            throw new ArgumentException("Replenishment quantity must be greater than zero");

        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var item = await db.Resources.FindAsync([id], ct);
        if (item == null) return null;

        item.AvailableQuantity += dto.Quantity;
        item.TotalQuantity += dto.Quantity;
        if (!string.IsNullOrWhiteSpace(dto.BatchNumber)) item.BatchNumber = dto.BatchNumber.Trim();
        if (dto.ExpiresAt.HasValue) item.ExpiresAt = dto.ExpiresAt.Value;
        item.UpdatedAt = DateTime.UtcNow;

        db.StockMovements.Add(new StockMovement
        {
            ResourceId = item.Id,
            MovementType = "Stock Received",
            Quantity = dto.Quantity,
            Notes = $"Stock replenished: +{dto.Quantity} {item.Unit}. Notes: {dto.Notes}",
            PerformedBy = actorId,
            CreatedAt = DateTime.UtcNow
        });

        TraceLog.Add(db, "Resource", item.Id, "Replenished", actorId, new { AddedQuantity = dto.Quantity, NewAvailable = item.AvailableQuantity });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return item;
    }

    public async Task<ResourceItem?> AdjustStockAsync(Guid id, AdjustStockDto dto, Guid actorId, CancellationToken ct = default)
    {
        if (dto.NewQuantity < 0)
            throw new ArgumentException("New quantity cannot be negative");

        string[] allowedMovements = ["Stock Adjusted", "Stock Damaged", "Stock Expired", "Stock Returned"];
        string movementType = allowedMovements.Contains(dto.MovementType) ? dto.MovementType : "Stock Adjusted";

        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var item = await db.Resources.FindAsync([id], ct);
        if (item == null) return null;

        int difference = dto.NewQuantity - item.AvailableQuantity;
        item.AvailableQuantity = dto.NewQuantity;
        if (dto.NewQuantity > item.TotalQuantity) item.TotalQuantity = dto.NewQuantity;
        item.UpdatedAt = DateTime.UtcNow;

        if (movementType == "Stock Damaged") item.Condition = "Damaged";
        if (movementType == "Stock Expired") item.Condition = "Expired";

        db.StockMovements.Add(new StockMovement
        {
            ResourceId = item.Id,
            MovementType = movementType,
            Quantity = Math.Abs(difference),
            Notes = $"Stock adjustment: delta {difference:+0;-0;0} {item.Unit} -> new balance {item.AvailableQuantity}. Reason: {dto.Reason}",
            PerformedBy = actorId,
            CreatedAt = DateTime.UtcNow
        });

        TraceLog.Add(db, "Resource", item.Id, "Adjusted", actorId, new { Previous = item.AvailableQuantity - difference, New = item.AvailableQuantity, Reason = dto.Reason });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return item;
    }

    public async Task<List<StockMovement>> GetMovementsAsync(Guid? resourceId, CancellationToken ct = default)
    {
        IQueryable<StockMovement> q = db.StockMovements.AsNoTracking();
        if (resourceId.HasValue) q = q.Where(x => x.ResourceId == resourceId.Value);
        return await q.OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct);
    }

    public async Task<List<ResourceItem>> GetLowStockAsync(int threshold = 10, CancellationToken ct = default) =>
        await db.Resources.AsNoTracking()
            .Where(r => r.IsActive && r.AvailableQuantity <= threshold)
            .OrderBy(r => r.AvailableQuantity)
            .ToListAsync(ct);

    public async Task<List<ResourceItem>> GetExpiringAsync(int days = 14, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(days);
        return await db.Resources.AsNoTracking()
            .Where(r => r.IsActive && r.ExpiresAt.HasValue && r.ExpiresAt.Value <= cutoff && r.ExpiresAt.Value >= DateTime.UtcNow)
            .OrderBy(r => r.ExpiresAt)
            .ToListAsync(ct);
    }

    public async Task<object> GetLogisticsDashboardMetricsAsync(CancellationToken ct = default)
    {
        var resources = await db.Resources.AsNoTracking().Where(r => r.IsActive).ToListAsync(ct);
        var dispatches = await db.Dispatches.AsNoTracking().ToListAsync(ct);
        var allocations = await db.ResourceAllocations.AsNoTracking().ToListAsync(ct);
        var requests = await db.Requests.AsNoTracking().Where(r => r.Status == "Verified" || r.Status == "Assigned").ToListAsync(ct);

        int totalResourceTypes = resources.Count;
        int totalAvailableStock = resources.Sum(r => r.AvailableQuantity);
        int totalReservedStock = resources.Sum(r => r.ReservedQuantity);
        int pendingAllocations = allocations.Count(a => a.Status == "Reserved");
        int activeDispatches = dispatches.Count(d => d.DeliveryStatus is "Dispatched" or "InTransit" or "ReadyForDispatch");

        int lowStockCount = resources.Count(r => r.AvailableQuantity <= 15);
        var cutoff14 = DateTime.UtcNow.AddDays(14);
        int expiringCount = resources.Count(r => r.ExpiresAt.HasValue && r.ExpiresAt.Value <= cutoff14 && r.ExpiresAt.Value >= DateTime.UtcNow);

        // Group by category
        var stockByCategory = resources.GroupBy(r => r.Category).Select(g => new
        {
            Category = g.Key,
            Available = g.Sum(r => r.AvailableQuantity),
            Reserved = g.Sum(r => r.ReservedQuantity),
            Total = g.Sum(r => r.TotalQuantity),
            ItemCount = g.Count()
        }).OrderByDescending(x => x.Total).ToList();

        // Calculate shortages across verified pending requests
        var shortages = requests.GroupBy(r => r.Category).Select(g =>
        {
            int required = g.Sum(r => r.RequestedQuantity);
            int available = resources.Where(x => x.Category.Equals(g.Key, StringComparison.OrdinalIgnoreCase)).Sum(x => x.AvailableQuantity);
            int deficit = Math.Max(0, required - available);
            return new
            {
                Category = g.Key,
                RequiredUnits = required,
                AvailableUnits = available,
                ShortageUnits = deficit,
                PendingCases = g.Count()
            };
        }).Where(x => x.ShortageUnits > 0).ToList();

        // Delivery pipeline counts
        var deliveryPipeline = new
        {
            ReadyForDispatch = dispatches.Count(d => d.DeliveryStatus == "ReadyForDispatch"),
            InTransit = dispatches.Count(d => d.DeliveryStatus is "Dispatched" or "InTransit"),
            Delivered = dispatches.Count(d => d.DeliveryStatus is "Delivered" or "Completed"),
            PartiallyDelivered = dispatches.Count(d => d.DeliveryStatus == "PartiallyDelivered"),
            Cancelled = dispatches.Count(d => d.DeliveryStatus == "Cancelled")
        };

        return new
        {
            TotalResourceTypes = totalResourceTypes,
            TotalAvailableStock = totalAvailableStock,
            TotalReservedStock = totalReservedStock,
            PendingAllocations = pendingAllocations,
            ActiveDispatches = activeDispatches,
            LowStockCount = lowStockCount,
            ExpiringCount = expiringCount,
            OutstandingShortagesCount = shortages.Count,
            Shortages = shortages,
            StockByCategory = stockByCategory,
            DeliveryPipeline = deliveryPipeline
        };
    }
}
