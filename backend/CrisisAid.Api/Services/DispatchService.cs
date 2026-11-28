using CrisisAid.Api.Contracts;
using CrisisAid.Api.Data;
using CrisisAid.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrisisAid.Api.Services;

public interface IDispatchService
{
    Task<List<object>> GetDispatchesAsync(string? status, CancellationToken ct = default);
    Task<object?> GetDispatchByIdAsync(Guid id, CancellationToken ct = default);
    Task<DispatchRecord> CreateDispatchAsync(CreateDispatchDto dto, Guid actorId, string baseUrl, CancellationToken ct = default);
    Task<DispatchRecord?> UpdateDispatchStatusAsync(Guid id, UpdateDispatchStatusDto dto, Guid actorId, CancellationToken ct = default);
    Task<object> CancelAllocationAsync(Guid allocationId, string reason, Guid actorId, CancellationToken ct = default);
}

public class DispatchService(AidDbContext db, IDeliveryVerificationService verificationService) : IDispatchService
{
    public async Task<List<object>> GetDispatchesAsync(string? status, CancellationToken ct = default)
    {
        IQueryable<DispatchRecord> q = db.Dispatches.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status) && status != "All")
            q = q.Where(d => d.DeliveryStatus == status);

        var list = await q.OrderByDescending(d => d.CreatedAt).Take(150).ToListAsync(ct);
        var allocationIds = list.Select(d => d.AllocationId).Distinct().ToList();
        var allocations = await db.ResourceAllocations.AsNoTracking().Where(a => allocationIds.Contains(a.Id)).ToListAsync(ct);
        var resourceIds = allocations.Select(a => a.ResourceId).Distinct().ToList();
        var resources = await db.Resources.AsNoTracking().Where(r => resourceIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);
        var requestIds = allocations.Select(a => a.RequestId).Distinct().ToList();
        var requests = await db.Requests.AsNoTracking().Where(r => requestIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);
        var dispatchIds = list.Select(d => d.Id).ToList();
        var verifications = await db.DeliveryVerifications.AsNoTracking()
            .Where(v => dispatchIds.Contains(v.DispatchId))
            .ToDictionaryAsync(v => v.DispatchId, ct);

        return list.Select(d =>
        {
            var alloc = allocations.FirstOrDefault(a => a.Id == d.AllocationId);
            var res = alloc != null && resources.TryGetValue(alloc.ResourceId, out var r) ? r : null;
            var req = alloc != null && requests.TryGetValue(alloc.RequestId, out var rq) ? rq : null;
            var ver = verifications.TryGetValue(d.Id, out var v) ? v : null;

            return new
            {
                d.Id,
                d.AllocationId,
                d.VehicleId,
                d.AssignedDriver,
                d.AssignedVolunteerId,
                d.DepartureLocation,
                d.Destination,
                d.DepartureTime,
                d.EstimatedArrivalTime,
                d.ActualArrivalTime,
                d.DispatchedQuantity,
                d.DeliveredQuantity,
                d.DeliveryStatus,
                d.DeliveryNotes,
                d.CreatedAt,
                d.UpdatedAt,
                ItemName = res?.Name ?? "Aid Supplies",
                ItemCategory = res?.Category ?? req?.Category ?? "Aid",
                ItemUnit = res?.Unit ?? "units",
                RequestId = alloc?.RequestId,
                RequestUrgency = req?.Urgency,
                VerificationShortCode = ver?.ShortCode,
                VerificationToken = ver?.VerificationToken,
                VerificationStatus = ver?.Status
            };
        }).Cast<object>().ToList();
    }

    public async Task<object?> GetDispatchByIdAsync(Guid id, CancellationToken ct = default)
    {
        var d = await db.Dispatches.FindAsync([id], ct);
        if (d == null) return null;

        var alloc = await db.ResourceAllocations.FindAsync([d.AllocationId], ct);
        var res = alloc != null ? await db.Resources.FindAsync([alloc.ResourceId], ct) : null;
        var req = alloc != null ? await db.Requests.FindAsync([alloc.RequestId], ct) : null;
        var ver = await db.DeliveryVerifications.FirstOrDefaultAsync(v => v.DispatchId == d.Id, ct);

        return new
        {
            d.Id,
            d.AllocationId,
            d.VehicleId,
            d.AssignedDriver,
            d.AssignedVolunteerId,
            d.DepartureLocation,
            d.Destination,
            d.DepartureTime,
            d.EstimatedArrivalTime,
            d.ActualArrivalTime,
            d.DispatchedQuantity,
            d.DeliveredQuantity,
            d.DeliveryStatus,
            d.DeliveryNotes,
            d.CreatedAt,
            d.UpdatedAt,
            Resource = res != null ? new { res.Id, res.Name, res.Category, res.Unit, res.Area } : null,
            Request = req != null ? new { req.Id, req.Category, req.Urgency, req.PeopleAffected, req.RequestedQuantity } : null,
            Verification = ver != null ? new { ver.Id, ver.ShortCode, ver.VerificationToken, ver.Status, ver.ExpiresAt, ver.QrCodeSvg } : null
        };
    }

    public async Task<DispatchRecord> CreateDispatchAsync(CreateDispatchDto dto, Guid actorId, string baseUrl, CancellationToken ct = default)
    {
        if (dto.DispatchedQuantity <= 0)
            throw new ArgumentException("Dispatched quantity must be greater than zero");

        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        var allocation = await db.ResourceAllocations.FindAsync([dto.AllocationId], ct);
        if (allocation == null)
            throw new InvalidOperationException("Allocation record not found");

        if (allocation.Status is not ("Reserved" or "ReadyForDispatch"))
            throw new InvalidOperationException($"Cannot dispatch allocation with status '{allocation.Status}'");

        var resource = await db.Resources.FindAsync([allocation.ResourceId], ct);
        if (resource == null)
            throw new InvalidOperationException("Resource not found");

        if (dto.DispatchedQuantity > allocation.Quantity)
            throw new InvalidOperationException($"Dispatched quantity ({dto.DispatchedQuantity}) cannot exceed reserved allocation quantity ({allocation.Quantity})");

        var dispatch = new DispatchRecord
        {
            AllocationId = allocation.Id,
            VehicleId = dto.VehicleId.Trim(),
            AssignedDriver = dto.AssignedDriver.Trim(),
            AssignedVolunteerId = dto.AssignedVolunteerId,
            DepartureLocation = dto.DepartureLocation.Trim(),
            Destination = dto.Destination.Trim(),
            DepartureTime = DateTime.UtcNow,
            EstimatedArrivalTime = dto.EstimatedArrivalTime ?? DateTime.UtcNow.AddHours(2),
            DispatchedQuantity = dto.DispatchedQuantity,
            DeliveredQuantity = 0,
            DeliveryStatus = "Dispatched",
            DeliveryNotes = dto.Notes?.Trim() ?? "",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Dispatches.Add(dispatch);

        // Update allocation
        allocation.Status = "Dispatched";
        allocation.DispatchedAt = DateTime.UtcNow;

        // Record stock dispatched movement
        db.StockMovements.Add(new StockMovement
        {
            ResourceId = resource.Id,
            MovementType = "Stock Dispatched",
            Quantity = dto.DispatchedQuantity,
            ReferenceId = dispatch.Id,
            Notes = $"Dispatched {dto.DispatchedQuantity} {resource.Unit} via {dto.VehicleId} (Driver: {dto.AssignedDriver}) to {dto.Destination}",
            PerformedBy = actorId,
            CreatedAt = DateTime.UtcNow
        });

        // Update request status if linked
        var request = await db.Requests.FindAsync([allocation.RequestId], ct);
        if (request != null)
        {
            request.Status = "Dispatched";
            request.UpdatedAt = DateTime.UtcNow;
            TraceLog.History(db, request.Id, actorId, "Dispatched", $"Aid in transit ({dto.DispatchedQuantity} {resource.Unit}) via {dto.VehicleId}.");
            TraceLog.Queue(db, request.CitizenId, $"dispatch:{dispatch.Id}", "Aid Dispatched", $"Your assistance is on the way via {dto.VehicleId}.");
        }

        TraceLog.Add(db, "DispatchRecord", dispatch.Id, "Dispatched", actorId, new { dto.VehicleId, dto.DispatchedQuantity });
        await db.SaveChangesAsync(ct);

        // Generate QR code and verification token immediately for recipient confirmation
        await verificationService.CreateForDispatchAsync(dispatch, db, baseUrl, ct);

        await tx.CommitAsync(ct);
        return dispatch;
    }

    public async Task<DispatchRecord?> UpdateDispatchStatusAsync(Guid id, UpdateDispatchStatusDto dto, Guid actorId, CancellationToken ct = default)
    {
        string[] allowed = ["ReadyForDispatch", "Dispatched", "InTransit", "Delivered", "PartiallyDelivered", "Failed", "Cancelled", "Completed"];
        if (!allowed.Contains(dto.Status))
            throw new ArgumentException($"Invalid dispatch status '{dto.Status}'");

        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        var dispatch = await db.Dispatches.FindAsync([id], ct);
        if (dispatch == null) return null;

        string previousStatus = dispatch.DeliveryStatus;
        dispatch.DeliveryStatus = dto.Status;
        dispatch.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto.Notes)) dispatch.DeliveryNotes = dto.Notes.Trim();

        var allocation = await db.ResourceAllocations.FindAsync([dispatch.AllocationId], ct);

        if (dto.Status == "InTransit")
        {
            dispatch.DepartureTime ??= DateTime.UtcNow;
        }
        else if (dto.Status is "Delivered" or "Completed")
        {
            dispatch.ActualArrivalTime = DateTime.UtcNow;
            int confirmed = dto.DeliveredQuantity ?? dispatch.DispatchedQuantity;
            dispatch.DeliveredQuantity = confirmed;

            if (allocation != null)
            {
                allocation.Status = "Delivered";
                var resource = await db.Resources.FindAsync([allocation.ResourceId], ct);
                if (resource != null)
                {
                    resource.ReservedQuantity = Math.Max(0, resource.ReservedQuantity - dispatch.DispatchedQuantity);
                    db.StockMovements.Add(new StockMovement
                    {
                        ResourceId = resource.Id,
                        MovementType = "Stock Delivered",
                        Quantity = confirmed,
                        ReferenceId = dispatch.Id,
                        Notes = $"Manual status delivery completion: {confirmed} {resource.Unit}. Notes: {dto.Notes}",
                        PerformedBy = actorId,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                var request = await db.Requests.FindAsync([allocation.RequestId], ct);
                if (request != null && confirmed >= request.RequestedQuantity)
                {
                    request.Status = "Fulfilled";
                    request.UpdatedAt = DateTime.UtcNow;
                    TraceLog.History(db, request.Id, actorId, "Fulfilled", "Delivery completed and verified.");
                    TraceLog.Queue(db, request.CitizenId, $"request:{request.Id}:fulfilled", "Aid delivered", "Your assistance has been successfully delivered.");
                }
            }
        }
        else if (dto.Status is "Failed" or "Cancelled")
        {
            // Reconcile and return stock
            if (allocation != null)
            {
                allocation.Status = dto.Status;
                var resource = await db.Resources.FindAsync([allocation.ResourceId], ct);
                if (resource != null)
                {
                    resource.ReservedQuantity = Math.Max(0, resource.ReservedQuantity - dispatch.DispatchedQuantity);
                    resource.AvailableQuantity += dispatch.DispatchedQuantity;
                    resource.UpdatedAt = DateTime.UtcNow;

                    db.StockMovements.Add(new StockMovement
                    {
                        ResourceId = resource.Id,
                        MovementType = "Stock Returned",
                        Quantity = dispatch.DispatchedQuantity,
                        ReferenceId = dispatch.Id,
                        Notes = $"Dispatch {dto.Status}: {dispatch.DispatchedQuantity} {resource.Unit} restored to available inventory. Notes: {dto.Notes}",
                        PerformedBy = actorId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        TraceLog.Add(db, "DispatchRecord", dispatch.Id, "StatusUpdated", actorId, new { Previous = previousStatus, New = dto.Status, dto.Notes });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return dispatch;
    }

    public async Task<object> CancelAllocationAsync(Guid allocationId, string reason, Guid actorId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        var allocation = await db.ResourceAllocations.FindAsync([allocationId], ct);
        if (allocation == null)
            throw new InvalidOperationException("Allocation not found");

        if (allocation.Status is "Dispatched" or "InTransit" or "Delivered" or "Completed")
            throw new InvalidOperationException($"Cannot cancel allocation that is already {allocation.Status}. Process through dispatch return instead.");

        var resource = await db.Resources.FindAsync([allocation.ResourceId], ct);
        if (resource == null)
            throw new InvalidOperationException("Resource not found");

        // Release reserved stock back to available
        resource.AvailableQuantity += allocation.Quantity;
        resource.ReservedQuantity = Math.Max(0, resource.ReservedQuantity - allocation.Quantity);
        resource.UpdatedAt = DateTime.UtcNow;

        allocation.Status = "Cancelled";
        allocation.Notes = string.IsNullOrWhiteSpace(allocation.Notes) ? $"Cancelled: {reason}" : $"{allocation.Notes} | Cancelled: {reason}";

        // Log return movement
        db.StockMovements.Add(new StockMovement
        {
            ResourceId = resource.Id,
            MovementType = "Stock Returned",
            Quantity = allocation.Quantity,
            ReferenceId = allocation.Id,
            Notes = $"Allocation cancelled. {allocation.Quantity} {resource.Unit} released back to inventory. Reason: {reason}",
            PerformedBy = actorId,
            CreatedAt = DateTime.UtcNow
        });

        // Revert request status if no other active allocations exist
        var request = await db.Requests.FindAsync([allocation.RequestId], ct);
        if (request != null)
        {
            bool hasOtherActive = await db.ResourceAllocations
                .AnyAsync(a => a.RequestId == request.Id && a.Id != allocation.Id && a.Status != "Cancelled", ct);
            if (!hasOtherActive)
            {
                request.Status = "Verified";
                request.UpdatedAt = DateTime.UtcNow;
            }
            TraceLog.History(db, request.Id, actorId, "Allocation Cancelled", $"{allocation.Quantity} {resource.Unit} reservation cancelled. Reason: {reason}");
        }

        TraceLog.Add(db, "ResourceAllocation", allocation.Id, "Cancelled", actorId, new { allocation.Quantity, reason });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new { Success = true, AllocationId = allocation.Id, Status = "Cancelled", ReleasedQuantity = allocation.Quantity };
    }
}
