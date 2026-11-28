using CrisisAid.Api.Contracts;
using CrisisAid.Api.Data;
using CrisisAid.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrisisAid.Api.Services;

public interface IPartnerCoordinationService
{
    Task<List<PartnerResourceRequest>> GetRequestsAsync(Guid? organisationId, CancellationToken ct = default);
    Task<PartnerResourceRequest> CreateRequestAsync(CreatePartnerResourceRequestDto dto, Guid userOrgId, Guid actorId, CancellationToken ct = default);
    Task<PartnerResourceRequest?> UpdateRequestStatusAsync(Guid id, string status, Guid actorId, CancellationToken ct = default);

    Task<List<object>> GetTransfersAsync(Guid? userOrgId, bool isStaff, CancellationToken ct = default);
    Task<StockTransfer> CreateTransferAsync(CreateStockTransferDto dto, Guid fromOrgId, Guid actorId, CancellationToken ct = default);
    Task<StockTransfer?> UpdateTransferStatusAsync(Guid id, string status, Guid actorId, CancellationToken ct = default);
    Task<object> ReceiveTransferAsync(Guid id, ReceiveStockTransferDto dto, Guid actorId, CancellationToken ct = default);
}

public class PartnerCoordinationService(AidDbContext db) : IPartnerCoordinationService
{
    public async Task<List<PartnerResourceRequest>> GetRequestsAsync(Guid? organisationId, CancellationToken ct = default)
    {
        IQueryable<PartnerResourceRequest> q = db.PartnerRequests.AsNoTracking();
        if (organisationId.HasValue)
        {
            q = q.Where(x => x.TargetOrganisationId == organisationId.Value || x.RequestedByOrganisationId == organisationId.Value);
        }
        return await q.OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct);
    }

    public async Task<PartnerResourceRequest> CreateRequestAsync(CreatePartnerResourceRequestDto dto, Guid userOrgId, Guid actorId, CancellationToken ct = default)
    {
        if (dto.Quantity <= 0)
            throw new ArgumentException("Requested quantity must be positive");

        if (!await db.Organisations.AnyAsync(o => o.Id == dto.TargetOrganisationId, ct))
            throw new InvalidOperationException("Target partner organisation not found");

        var req = new PartnerResourceRequest
        {
            TargetOrganisationId = dto.TargetOrganisationId,
            RequestedByOrganisationId = userOrgId,
            CreatedBy = actorId,
            Category = dto.Category.Trim(),
            Quantity = dto.Quantity,
            Urgency = string.IsNullOrWhiteSpace(dto.Urgency) ? "Medium" : dto.Urgency.Trim(),
            Notes = dto.Notes?.Trim() ?? "",
            Status = "Requested",
            CreatedAt = DateTime.UtcNow
        };

        db.PartnerRequests.Add(req);
        TraceLog.Add(db, "PartnerResourceRequest", req.Id, "Requested", actorId, new { dto.Category, dto.Quantity, dto.TargetOrganisationId });
        await db.SaveChangesAsync(ct);
        return req;
    }

    public async Task<PartnerResourceRequest?> UpdateRequestStatusAsync(Guid id, string status, Guid actorId, CancellationToken ct = default)
    {
        string[] valid = ["Requested", "Acknowledged", "Fulfilled", "Declined"];
        if (!valid.Contains(status))
            throw new ArgumentException($"Invalid status '{status}'");

        var req = await db.PartnerRequests.FindAsync([id], ct);
        if (req == null) return null;

        req.Status = status;
        TraceLog.Add(db, "PartnerResourceRequest", id, status, actorId);
        await db.SaveChangesAsync(ct);
        return req;
    }

    public async Task<List<object>> GetTransfersAsync(Guid? userOrgId, bool isStaff, CancellationToken ct = default)
    {
        IQueryable<StockTransfer> q = db.StockTransfers.AsNoTracking();
        if (!isStaff && userOrgId.HasValue)
        {
            q = q.Where(t => t.FromOrganisationId == userOrgId.Value || t.ToOrganisationId == userOrgId.Value);
        }

        var list = await q.OrderByDescending(t => t.CreatedAt).Take(100).ToListAsync(ct);
        var orgIds = list.Select(t => t.FromOrganisationId).Concat(list.Select(t => t.ToOrganisationId)).Distinct().ToList();
        var orgs = await db.Organisations.AsNoTracking().Where(o => orgIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Name, ct);

        return list.Select(t => new
        {
            t.Id,
            t.PartnerOfferId,
            t.FromOrganisationId,
            FromOrganisationName = orgs.TryGetValue(t.FromOrganisationId, out var fromName) ? fromName : "Unknown",
            t.ToOrganisationId,
            ToOrganisationName = orgs.TryGetValue(t.ToOrganisationId, out var toName) ? toName : "Unknown",
            t.Category,
            t.ResourceName,
            t.Quantity,
            t.Status,
            t.SentAt,
            t.ReceivedAt,
            t.DiscrepancyNotes,
            t.CreatedAt
        }).Cast<object>().ToList();
    }

    public async Task<StockTransfer> CreateTransferAsync(CreateStockTransferDto dto, Guid fromOrgId, Guid actorId, CancellationToken ct = default)
    {
        if (dto.Quantity <= 0)
            throw new ArgumentException("Transfer quantity must be positive");

        if (!await db.Organisations.AnyAsync(o => o.Id == dto.ToOrganisationId, ct))
            throw new InvalidOperationException("Destination organisation not found");

        var transfer = new StockTransfer
        {
            PartnerOfferId = dto.PartnerOfferId,
            FromOrganisationId = fromOrgId,
            ToOrganisationId = dto.ToOrganisationId,
            Category = dto.Category.Trim(),
            ResourceName = dto.ResourceName.Trim(),
            Quantity = dto.Quantity,
            Status = "Accepted",
            SentAt = DateTime.UtcNow,
            TargetResourceId = dto.TargetResourceId,
            CreatedAt = DateTime.UtcNow
        };

        db.StockTransfers.Add(transfer);
        TraceLog.Add(db, "StockTransfer", transfer.Id, "Initiated", actorId, new { dto.Category, dto.Quantity, dto.ToOrganisationId });
        await db.SaveChangesAsync(ct);
        return transfer;
    }

    public async Task<StockTransfer?> UpdateTransferStatusAsync(Guid id, string status, Guid actorId, CancellationToken ct = default)
    {
        string[] valid = ["Offered", "Accepted", "InTransit", "Received", "Cancelled"];
        if (!valid.Contains(status))
            throw new ArgumentException($"Invalid status '{status}'");

        var transfer = await db.StockTransfers.FindAsync([id], ct);
        if (transfer == null) return null;

        transfer.Status = status;
        if (status == "InTransit" && !transfer.SentAt.HasValue) transfer.SentAt = DateTime.UtcNow;

        TraceLog.Add(db, "StockTransfer", id, status, actorId);
        await db.SaveChangesAsync(ct);
        return transfer;
    }

    public async Task<object> ReceiveTransferAsync(Guid id, ReceiveStockTransferDto dto, Guid actorId, CancellationToken ct = default)
    {
        if (dto.ConfirmedQuantity <= 0)
            throw new ArgumentException("Confirmed received quantity must be greater than zero");

        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        var transfer = await db.StockTransfers.FindAsync([id], ct);
        if (transfer == null)
            throw new InvalidOperationException("Stock transfer record not found");

        if (transfer.Status == "Received")
            throw new InvalidOperationException("Transfer has already been received and credited");

        transfer.Status = "Received";
        transfer.ReceivedAt = DateTime.UtcNow;
        transfer.ReceivedBy = actorId;
        transfer.DiscrepancyNotes = dto.DiscrepancyNotes?.Trim() ?? "";

        // Credit to physical inventory
        ResourceItem? targetResource = null;
        if (dto.CreditResourceId.HasValue)
        {
            targetResource = await db.Resources.FindAsync([dto.CreditResourceId.Value], ct);
        }

        if (targetResource == null)
        {
            // Find existing matching resource batch or create new batch
            targetResource = await db.Resources.FirstOrDefaultAsync(r =>
                r.OrganisationId == transfer.ToOrganisationId &&
                r.Category == transfer.Category &&
                r.Name == transfer.ResourceName &&
                r.IsActive, ct);
        }

        if (targetResource != null)
        {
            targetResource.AvailableQuantity += dto.ConfirmedQuantity;
            targetResource.TotalQuantity += dto.ConfirmedQuantity;
            targetResource.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // Create new inventory batch for received transfer
            targetResource = new ResourceItem
            {
                Name = transfer.ResourceName,
                Category = transfer.Category,
                Unit = "units",
                AvailableQuantity = dto.ConfirmedQuantity,
                TotalQuantity = dto.ConfirmedQuantity,
                ReservedQuantity = 0,
                Area = "Central Warehouse",
                Description = $"Inter-agency transfer from partner organisation. Discrepancy: {dto.DiscrepancyNotes}",
                Condition = "Good",
                BatchNumber = $"TRANSFER-{transfer.Id.ToString()[..8].ToUpper()}",
                OrganisationId = transfer.ToOrganisationId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Resources.Add(targetResource);
        }

        // Record Stock Received movement
        db.StockMovements.Add(new StockMovement
        {
            ResourceId = targetResource.Id,
            MovementType = "Stock Received",
            Quantity = dto.ConfirmedQuantity,
            ReferenceId = transfer.Id,
            Notes = $"Partner transfer received: +{dto.ConfirmedQuantity} {targetResource.Unit} from {transfer.FromOrganisationId}. Notes: {dto.DiscrepancyNotes}",
            PerformedBy = actorId,
            CreatedAt = DateTime.UtcNow
        });

        TraceLog.Add(db, "StockTransfer", transfer.Id, "Received", actorId, new { ConfirmedQuantity = dto.ConfirmedQuantity, TargetResourceId = targetResource.Id });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new
        {
            Success = true,
            TransferId = transfer.Id,
            ConfirmedQuantity = dto.ConfirmedQuantity,
            CreditedResourceId = targetResource.Id,
            NewAvailableQuantity = targetResource.AvailableQuantity
        };
    }
}
