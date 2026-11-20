using CrisisAid.Api.Data;
using CrisisAid.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrisisAid.Api.Services;

// Repository: hides the case read persistence query and provides an easily mocked boundary.
public interface IRequestRepository
{
    Task<List<AssistanceRequest>> ListVisibleAsync(Guid actorId, bool isStaff, string? status, Guid? crisisId, CancellationToken ct);
    Task<AssistanceRequest?> FindAsync(Guid id, CancellationToken ct);
}
public class EfRequestRepository(AidDbContext db) : IRequestRepository
{
    public Task<AssistanceRequest?> FindAsync(Guid id, CancellationToken ct) =>
        db.Requests.FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<List<AssistanceRequest>> ListVisibleAsync(Guid actorId, bool isStaff, string? status, Guid? crisisId, CancellationToken ct)
    {
        IQueryable<AssistanceRequest> q = db.Requests.AsNoTracking();
        if (!isStaff) q = q.Where(x => x.CitizenId == actorId);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);
        if (crisisId != null) q = q.Where(x => x.CrisisId == crisisId);
        return q.OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct);
    }
}

// Strategy and Factory: alternate full-stock-only versus shortage-aware matching policies.
public interface IResourceMatchStrategy { IEnumerable<object> Match(AssistanceRequest request, IEnumerable<ResourceItem> stock); }
public sealed class AllowPartialMatchStrategy : IResourceMatchStrategy
{
    public IEnumerable<object> Match(AssistanceRequest request, IEnumerable<ResourceItem> stock) => MatchEngine.ResourcesFor(request, stock);
}
public sealed class FullQuantityMatchStrategy : IResourceMatchStrategy
{
    public IEnumerable<object> Match(AssistanceRequest request, IEnumerable<ResourceItem> stock) =>
        MatchEngine.ResourcesFor(request, stock.Where(x => x.AvailableQuantity >= request.RequestedQuantity));
}
public interface IResourceMatchStrategyFactory { IResourceMatchStrategy Create(bool allowPartial); }
public sealed class ResourceMatchStrategyFactory : IResourceMatchStrategyFactory
{
    public IResourceMatchStrategy Create(bool allowPartial) => allowPartial ? new AllowPartialMatchStrategy() : new FullQuantityMatchStrategy();
}
