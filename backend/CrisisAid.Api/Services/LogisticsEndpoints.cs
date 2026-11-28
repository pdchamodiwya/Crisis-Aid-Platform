using CrisisAid.Api.Contracts;
using CrisisAid.Api.Data;
using CrisisAid.Api.Domain;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace CrisisAid.Api.Services;

public static class LogisticsEndpoints
{
    public static void Map(RouteGroupBuilder api, WebApplication app)
    {
        // -------------------------------------------------------------
        // Public / Recipient Delivery Verification Endpoints (AllowAnonymous)
        // -------------------------------------------------------------
        app.MapGet("/api/delivery-verify/{token}", async (string token, IDeliveryVerificationService verService, AidDbContext db, CancellationToken ct) =>
        {
            var details = await verService.GetVerificationDetailsAsync(token, db, ct);
            return details != null ? Results.Ok(details) : Results.NotFound(new { error = "Verification token not found or expired" });
        }).AllowAnonymous();

        app.MapPost("/api/delivery-verify/{token}/confirm", async (string token, VerifyQrDeliveryDto dto, HttpContext ctx, IDeliveryVerificationService verService, AidDbContext db, CancellationToken ct) =>
        {
            Guid? actorId = ctx.User.Identity?.IsAuthenticated == true ? Access.UserId(ctx.User) : null;
            var (success, error, result) = await verService.ConfirmDeliveryAsync(token, dto.DeliveredQuantity, dto.Notes ?? "", actorId, db, ct);
            return success ? Results.Ok(result) : Results.BadRequest(new { error });
        }).AllowAnonymous();

        // -------------------------------------------------------------
        // Inventory & Stock Management Endpoints
        // -------------------------------------------------------------
        api.MapGet("/logistics/dashboard", async (ILogisticsService logistics, CancellationToken ct) =>
            Results.Ok(await logistics.GetLogisticsDashboardMetricsAsync(ct)))
            .RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Admin));

        api.MapGet("/resources/low-stock", async (ILogisticsService logistics, int? threshold, CancellationToken ct) =>
            Results.Ok(await logistics.GetLowStockAsync(threshold ?? 15, ct)))
            .RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Admin));

        api.MapGet("/resources/expiring", async (ILogisticsService logistics, int? days, CancellationToken ct) =>
            Results.Ok(await logistics.GetExpiringAsync(days ?? 14, ct)))
            .RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Admin));

        api.MapGet("/resources/movements", async (ILogisticsService logistics, CancellationToken ct) =>
            Results.Ok(await logistics.GetMovementsAsync(null, ct)))
            .RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Admin));

        api.MapGet("/resources/{id:guid}/movements", async (Guid id, ILogisticsService logistics, CancellationToken ct) =>
            Results.Ok(await logistics.GetMovementsAsync(id, ct)))
            .RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Admin));

        api.MapGet("/resources/{id:guid}", async (Guid id, ILogisticsService logistics, CancellationToken ct) =>
            await logistics.GetResourceByIdAsync(id, ct) is { } item ? Results.Ok(item) : Results.NotFound())
            .RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Admin));

        api.MapPost("/resources/create", async (CreateResourceDto dto, HttpContext ctx, ILogisticsService logistics, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || dto.AvailableQuantity < 0 || !Access.Coordinates(dto.Latitude, dto.Longitude))
                return Results.BadRequest(new { error = "Valid name, non-negative quantity and coordinates required" });
            var created = await logistics.CreateResourceAsync(dto, Access.UserId(ctx.User), ct);
            return Results.Created($"/api/resources/{created.Id}", created);
        }).RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Admin));

        api.MapPut("/resources/{id:guid}", async (Guid id, UpdateResourceDto dto, HttpContext ctx, ILogisticsService logistics, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || !Access.Coordinates(dto.Latitude, dto.Longitude))
                return Results.BadRequest(new { error = "Valid name and coordinates required" });
            var updated = await logistics.UpdateResourceAsync(id, dto, Access.UserId(ctx.User), ct);
            return updated != null ? Results.Ok(updated) : Results.NotFound();
        }).RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Admin));

        api.MapDelete("/resources/{id:guid}", async (Guid id, HttpContext ctx, ILogisticsService logistics, CancellationToken ct) =>
        {
            try
            {
                bool deleted = await logistics.DeactivateResourceAsync(id, Access.UserId(ctx.User), ct);
                return deleted ? Results.NoContent() : Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Admin));

        api.MapPost("/resources/{id:guid}/replenish", async (Guid id, ReplenishResourceDto dto, HttpContext ctx, ILogisticsService logistics, CancellationToken ct) =>
        {
            if (dto.Quantity <= 0) return Results.BadRequest(new { error = "Replenishment quantity must be positive" });
            var item = await logistics.ReplenishStockAsync(id, dto, Access.UserId(ctx.User), ct);
            return item != null ? Results.Ok(item) : Results.NotFound();
        }).RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Admin));

        api.MapPost("/resources/{id:guid}/adjust", async (Guid id, AdjustStockDto dto, HttpContext ctx, ILogisticsService logistics, CancellationToken ct) =>
        {
            if (dto.NewQuantity < 0) return Results.BadRequest(new { error = "Quantity cannot be negative" });
            if (string.IsNullOrWhiteSpace(dto.Reason)) return Results.BadRequest(new { error = "Adjustment reason is required" });
            var item = await logistics.AdjustStockAsync(id, dto, Access.UserId(ctx.User), ct);
            return item != null ? Results.Ok(item) : Results.NotFound();
        }).RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Admin));

        // -------------------------------------------------------------
        // Shortage Resolution Endpoints
        // -------------------------------------------------------------
        api.MapGet("/resources/shortages/analysis", async (string? category, Guid? crisisId, IShortageResolutionService shortages, CancellationToken ct) =>
            Results.Ok(await shortages.AnalyzeShortagesAsync(category, crisisId, ct)))
            .RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Admin));

        api.MapPost("/resources/shortages/plan", async (ShortagePlanRequestDto dto, IShortageResolutionService shortages, CancellationToken ct) =>
            Results.Ok(await shortages.GenerateSuggestedPlanAsync(dto, ct)))
            .RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Admin));

        api.MapPost("/resources/shortages/resolve-plan", async (ResolveShortagePlanDto dto, HttpContext ctx, IShortageResolutionService shortages, CancellationToken ct) =>
        {
            try
            {
                var result = await shortages.ApplyShortagePlanAsync(dto, Access.UserId(ctx.User), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Admin));

        // -------------------------------------------------------------
        // Allocations & Dispatches Endpoints
        // -------------------------------------------------------------
        api.MapGet("/allocations/{id:guid}", async (Guid id, AidDbContext db, CancellationToken ct) =>
        {
            var alloc = await db.ResourceAllocations.FindAsync([id], ct);
            if (alloc == null) return Results.NotFound();
            var res = await db.Resources.FindAsync([alloc.ResourceId], ct);
            var req = await db.Requests.FindAsync([alloc.RequestId], ct);
            return Results.Ok(new { alloc, Resource = res, Request = req });
        }).RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Admin));

        api.MapPost("/allocations/{id:guid}/cancel", async (Guid id, HttpContext ctx, IDispatchService dispatchService, CancellationToken ct) =>
        {
            try
            {
                var result = await dispatchService.CancelAllocationAsync(id, "Cancelled by logistics coordinator", Access.UserId(ctx.User), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Admin));

        api.MapGet("/dispatches", async (string? status, IDispatchService dispatchService, CancellationToken ct) =>
            Results.Ok(await dispatchService.GetDispatchesAsync(status, ct)))
            .RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Volunteer, Roles.Admin));

        api.MapGet("/dispatches/{id:guid}", async (Guid id, IDispatchService dispatchService, CancellationToken ct) =>
            await dispatchService.GetDispatchByIdAsync(id, ct) is { } d ? Results.Ok(d) : Results.NotFound())
            .RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Volunteer, Roles.Admin));

        api.MapPost("/allocations/{id:guid}/dispatch", async (Guid id, CreateDispatchDto dto, HttpContext ctx, IConfiguration cfg, IDispatchService dispatchService, CancellationToken ct) =>
        {
            try
            {
                string baseUrl = cfg["Cors:Origins"]?.Split(',')[0] ?? "http://localhost:5173";
                var dispatch = await dispatchService.CreateDispatchAsync(dto with { AllocationId = id }, Access.UserId(ctx.User), baseUrl, ct);
                return Results.Created($"/api/dispatches/{dispatch.Id}", dispatch);
            }
            catch (Exception ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Admin));

        api.MapPatch("/dispatches/{id:guid}/status", async (Guid id, UpdateDispatchStatusDto dto, HttpContext ctx, IDispatchService dispatchService, CancellationToken ct) =>
        {
            try
            {
                var updated = await dispatchService.UpdateDispatchStatusAsync(id, dto, Access.UserId(ctx.User), ct);
                return updated != null ? Results.Ok(updated) : Results.NotFound();
            }
            catch (Exception ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Volunteer, Roles.Admin));

        api.MapGet("/dispatches/{id:guid}/qr", async (Guid id, AidDbContext db, IConfiguration cfg, IDeliveryVerificationService verService, CancellationToken ct) =>
        {
            var dispatch = await db.Dispatches.FindAsync([id], ct);
            if (dispatch == null) return Results.NotFound();
            string baseUrl = cfg["Cors:Origins"]?.Split(',')[0] ?? "http://localhost:5173";
            var ver = await verService.CreateForDispatchAsync(dispatch, db, baseUrl, ct);
            return Results.Ok(new { ver.Id, ver.DispatchId, ver.ShortCode, ver.VerificationToken, ver.QrCodeSvg, ver.Status, ver.ExpiresAt });
        }).RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Volunteer, Roles.Admin));

        api.MapPost("/dispatches/{id:guid}/confirm-delivery", async (Guid id, ConfirmDeliveryDto dto, HttpContext ctx, AidDbContext db, IDeliveryVerificationService verService, CancellationToken ct) =>
        {
            var ver = await db.DeliveryVerifications.FirstOrDefaultAsync(v => v.DispatchId == id && v.Status == "Pending", ct);
            if (ver != null)
            {
                var (success, error, result) = await verService.ConfirmDeliveryAsync(ver.VerificationToken, dto.DeliveredQuantity, dto.Notes ?? "", Access.UserId(ctx.User), db, ct);
                return success ? Results.Ok(result) : Results.Conflict(new { error });
            }
            return Results.BadRequest(new { error = "No active delivery verification found for dispatch" });
        }).RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Admin));

        // -------------------------------------------------------------
        // Partner Coordination (Requests & Stock Transfers)
        // -------------------------------------------------------------
        api.MapGet("/partners/requests", async (HttpContext ctx, AidDbContext db, IPartnerCoordinationService partnerService, CancellationToken ct) =>
        {
            var uid = Access.UserId(ctx.User);
            var userOrgId = await db.Users.Where(u => u.Id == uid).Select(u => u.OrganisationId).FirstOrDefaultAsync(ct);
            return Results.Ok(await partnerService.GetRequestsAsync(Access.Staff(ctx.User) ? null : userOrgId, ct));
        }).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.Partner, Roles.Logistics, Roles.Admin));

        api.MapPost("/partners/requests", async (CreatePartnerResourceRequestDto dto, HttpContext ctx, AidDbContext db, IPartnerCoordinationService partnerService, CancellationToken ct) =>
        {
            var uid = Access.UserId(ctx.User);
            var userOrgId = await db.Users.Where(u => u.Id == uid).Select(u => u.OrganisationId).FirstOrDefaultAsync(ct);
            if (!userOrgId.HasValue && !Access.Staff(ctx.User))
                return Results.BadRequest(new { error = "User is not assigned to an organisation" });

            try
            {
                var created = await partnerService.CreateRequestAsync(dto, userOrgId ?? dto.TargetOrganisationId, uid, ct);
                return Results.Created($"/api/partners/requests/{created.Id}", created);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.Partner, Roles.Logistics, Roles.Admin));

        api.MapPatch("/partners/requests/{id:guid}/status", async (Guid id, PartnerRequestStatusDto dto, HttpContext ctx, IPartnerCoordinationService partnerService, CancellationToken ct) =>
        {
            try
            {
                var updated = await partnerService.UpdateRequestStatusAsync(id, dto.Status, Access.UserId(ctx.User), ct);
                return updated != null ? Results.Ok(updated) : Results.NotFound();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.Partner, Roles.Logistics, Roles.Admin));

        api.MapGet("/partners/transfers", async (HttpContext ctx, AidDbContext db, IPartnerCoordinationService partnerService, CancellationToken ct) =>
        {
            var uid = Access.UserId(ctx.User);
            var userOrgId = await db.Users.Where(u => u.Id == uid).Select(u => u.OrganisationId).FirstOrDefaultAsync(ct);
            return Results.Ok(await partnerService.GetTransfersAsync(userOrgId, Access.Staff(ctx.User), ct));
        }).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.Partner, Roles.Logistics, Roles.Admin));

        api.MapPost("/partners/transfers", async (CreateStockTransferDto dto, HttpContext ctx, AidDbContext db, IPartnerCoordinationService partnerService, CancellationToken ct) =>
        {
            var uid = Access.UserId(ctx.User);
            var userOrgId = await db.Users.Where(u => u.Id == uid).Select(u => u.OrganisationId).FirstOrDefaultAsync(ct);
            if (!userOrgId.HasValue && !Access.Staff(ctx.User))
                return Results.BadRequest(new { error = "Sender organisation required" });

            try
            {
                var created = await partnerService.CreateTransferAsync(dto, userOrgId ?? dto.ToOrganisationId, uid, ct);
                return Results.Created($"/api/partners/transfers/{created.Id}", created);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.Partner, Roles.Logistics, Roles.Admin));

        api.MapPatch("/partners/transfers/{id:guid}/status", async (Guid id, PartnerTransferStatusDto dto, HttpContext ctx, IPartnerCoordinationService partnerService, CancellationToken ct) =>
        {
            try
            {
                var updated = await partnerService.UpdateTransferStatusAsync(id, dto.Status, Access.UserId(ctx.User), ct);
                return updated != null ? Results.Ok(updated) : Results.NotFound();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.Partner, Roles.Logistics, Roles.Admin));

        api.MapPost("/partners/transfers/{id:guid}/receive", async (Guid id, ReceiveStockTransferDto dto, HttpContext ctx, IPartnerCoordinationService partnerService, CancellationToken ct) =>
        {
            try
            {
                var result = await partnerService.ReceiveTransferAsync(id, dto, Access.UserId(ctx.User), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.Logistics, Roles.Admin));
    }

    public record PartnerRequestStatusDto(string Status);
    public record PartnerTransferStatusDto(string Status);
}
