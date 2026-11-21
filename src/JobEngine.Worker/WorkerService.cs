namespace JobEngine.Worker;

public class WorkerService(ILogger<WorkerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker host up. Job consuming starts in Phase 1.");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
            }
        }
        catch (OperationCanceledException)
        {
        }

        logger.LogInformation("Worker host stopping.");
    }
}
