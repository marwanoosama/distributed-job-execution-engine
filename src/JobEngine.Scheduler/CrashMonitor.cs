using JobEngine.Application.interfaces;
using JobEngine.Application.services.retry;
using JobEngine.Domain.models;
using Microsoft.EntityFrameworkCore;

namespace JobEngine.Scheduler;

public class CrashMonitor(IServiceScopeFactory scopes, ILeaseManager leases, IJobQueue queue,
    RetryPolicy retryPolicy, IConfiguration config, ILogger<CrashMonitor> log) : BackgroundService
{
    private const string LostError = "Worker lost: the lease expired";

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var every = TimeSpan.FromSeconds(config.GetValue("Monitor:IntervalSeconds", 5));
        using var timer = new PeriodicTimer(every);

        while (!stop.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stop)) break;

                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IJobDbContext>();

                await RecoverExpiredJobsAsync(db, stop);
                await MarkDeadWorkersAsync(db, stop);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Crash monitor error");
            }   
        }
    }

    private async Task RecoverExpiredJobsAsync(IJobDbContext db, CancellationToken ct)
    {
        var running = await db.Jobs.AsNoTracking()
            .Where(j => j.Status == JobStatus.Running && j.LeaseToken != null)
            .OrderBy(j => j.RunAt)
            .Select(j => new { j.Id, j.Priority, j.AttemptCount, j.MaxAttempts, Token = j.LeaseToken!.Value })
            .Take(500).ToListAsync(ct);

        foreach (var j in running)
        {
            if (await leases.ExistsAsync(j.Id)) continue; 
            await RecoverAsync(db, j.Id, j.Priority, j.AttemptCount, j.MaxAttempts, j.Token, ct);
        }
    }

    private async Task RecoverAsync(IJobDbContext db, Guid id, int priority, int attemptCount, int maxAttempts, Guid token, CancellationToken ct)
    {
        var workerId = await db.JobAttempts.AsNoTracking()
            .Where(a => a.JobId == id && a.FinishedAt == null)
            .Select(a => a.WorkerId)
            .FirstOrDefaultAsync(ct);

        int rows;
        DateTimeOffset? nextRunAt = null;

        if (attemptCount < maxAttempts)
        {
            var next = DateTimeOffset.UtcNow + retryPolicy.GetDelay(attemptCount);
            nextRunAt = next;
            rows = await db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE jobs SET status = 'Retrying', last_error = {LostError}, run_at = {next}, lease_token = NULL
                WHERE id = {id} AND lease_token = {token} AND status = 'Running'", ct);
        }
        else
        {
            rows = await db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE jobs SET status = 'Dead', last_error = {LostError}, lease_token = NULL
                WHERE id = {id} AND lease_token = {token} AND status = 'Running'", ct);
        }

        if (rows == 0) return;   
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE job_attempts SET finished_at = now(), result = 'LeaseExpired', error = {LostError}
            WHERE job_id = {id} AND finished_at IS NULL", ct);

        if (workerId is { } w) await queue.AcknowledgeAsync(w.ToString(), id);

        if (nextRunAt is { } at) await queue.ScheduleAsync(id, priority, at);
        else await queue.PushToDeadLetterAsync(id);

        log.LogWarning("Recovered job {Id}: lease expired, now {Outcome}", id, nextRunAt is null ? "Dead" : "Retrying");
    }

    private async Task MarkDeadWorkersAsync(IJobDbContext db, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-config.GetValue("Monitor:WorkerDeadAfterSeconds", 15));
        var rows = await db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE workers SET status = 'Dead'
            WHERE status = 'Alive' AND last_heartbeat < {cutoff}", ct);
        if (rows > 0) log.LogWarning("Marked {Count} worker(s) as Dead", rows);
    }
}