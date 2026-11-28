using CrisisAid.Api.Data;
using CrisisAid.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrisisAid.Api.Services;

public sealed class VolunteerSafetyWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<VolunteerSafetyWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = Math.Clamp(configuration.GetValue("VolunteerSafety:PollIntervalSeconds", 60), 10, 3600);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await NotifyOverdueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Volunteer safety check-in scan failed");
            }
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }

    private async Task NotifyOverdueAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AidDbContext>();
        var now = DateTime.UtcNow;
        var overdueTasks = await db.VolunteerTasks.Where(x =>
            (x.Status == "Accepted" || x.Status == "In Progress") &&
            x.NextCheckInDueAt != null && x.NextCheckInDueAt <= now &&
            x.OverdueAlertSentAt == null).ToListAsync(ct);
        if (overdueTasks.Count == 0) return;

        var coordinators = await db.Users.Where(x => x.IsActive &&
                (x.Role == Roles.VolunteerCoordinator || x.Role == Roles.Coordinator || x.Role == Roles.Admin))
            .Select(x => new { x.Id, x.DisplayName }).ToListAsync(ct);
        if (coordinators.Count == 0)
            logger.LogWarning("Overdue volunteer check-ins found but no active coordinators are configured");

        foreach (var task in overdueTasks)
        {
            task.OverdueAlertSentAt = now;
            var checkIn = new VolunteerCheckIn
            {
                TaskId = task.Id,
                VolunteerId = task.VolunteerId,
                At = now,
                Status = "Overdue"
            };
            db.VolunteerCheckIns.Add(checkIn);
            TraceLog.Add(db, "VolunteerCheckIn", checkIn.Id, "Overdue", null, new { task.Id, task.NextCheckInDueAt });
            foreach (var coordinator in coordinators)
            {
                TraceLog.Queue(db, coordinator.Id,
                    $"safety-overdue:{checkIn.Id:N}:{coordinator.Id:N}",
                    "Volunteer safety check-in overdue",
                    $"Volunteer assignment {task.Id:N} missed its scheduled safety check-in. Please follow up with the volunteer.");
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
