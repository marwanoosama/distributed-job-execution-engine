using JobEngine.Application.interfaces;
using Microsoft.EntityFrameworkCore;
using WorkerEntity = JobEngine.Domain.models.Worker;

namespace JobEngine.Worker;

public class WorkerHeartbeatService(IServiceScopeFactory scopes, WorkerEntity me, IConfiguration config, 
    ILogger<WorkerHeartbeatService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var every = TimeSpan.FromSeconds(config.GetValue("Worker:HeartbeatSeconds", 5));
        using var timer = new PeriodicTimer(every);

        while (!stop.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stop)) break;

                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IJobDbContext>();

                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE workers SET last_heartbeat = now(), status = 'Alive'
                    WHERE id = {me.Id} AND status <> 'Stopped'", stop);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Worker heartbeat failed");
            }
        }

    }
}