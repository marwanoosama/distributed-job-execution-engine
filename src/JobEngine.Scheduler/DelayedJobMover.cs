using JobEngine.Application.interfaces;

namespace JobEngine.Scheduler;

public class DelayedJobMover(IJobQueue queue, ILogger<DelayedJobMover> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (!stop.IsCancellationRequested)
        {
            try
            {
                // if one seconds already passed and we havent finished this iteration, then wait for current run to finsish then run the next one immediately
                if (!await timer.WaitForNextTickAsync(stop)) break;

                var moved = await queue.MoveDueJobsAsync(DateTimeOffset.UtcNow);
                if (moved > 0) log.LogInformation("Moved {Count} delayed jobs to the queue", moved);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
            catch (Exception ex) { log.LogError(ex, "Delayed mover error"); }
        }
    }
}