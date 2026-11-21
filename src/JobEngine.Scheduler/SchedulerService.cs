namespace JobEngine.Scheduler;

public class SchedulerService(ILogger<SchedulerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Scheduler host up. Background loops start in Phases 2-5.");

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

        logger.LogInformation("Scheduler host stopping.");
    }
}
