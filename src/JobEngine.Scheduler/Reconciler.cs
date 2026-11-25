using JobEngine.Application.interfaces;
using JobEngine.Domain.models;
using Microsoft.EntityFrameworkCore;

namespace JobEngine.Scheduler;

// if the api crashes after db insertion or redis loses data, then put the ojbs back in queue
public class Reconciler(IServiceScopeFactory scopes, ILogger<Reconciler> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stop))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IJobDbContext>();
                var queue = scope.ServiceProvider.GetRequiredService<IJobQueue>();

                var cutoff = DateTimeOffset.UtcNow.AddSeconds(-60);
                var stuck = await db.Jobs.AsNoTracking()
                    .Where(j => (j.Status == JobStatus.Pending || j.Status == JobStatus.Retrying) && j.RunAt <= cutoff)
                    .OrderBy(j => j.RunAt)
                    .Select(j => new { j.Id, j.Priority })
                    .Take(500).ToListAsync(stop);

                foreach (var j in stuck) await queue.EnqueueAsync(j.Id, j.Priority);
                if (stuck.Count > 0) log.LogInformation("Re-enqueued {Count} stuck jobs", stuck.Count);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Reconciler error");
                await DelayAsync(1000, stop);        
            }
        }
    }
    
    private static async Task DelayAsync(int ms, CancellationToken ct)
    {
        try { await Task.Delay(ms, ct); }
        catch (OperationCanceledException) { }
    }
}