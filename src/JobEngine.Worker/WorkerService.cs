using JobEngine.Application.interfaces;
using JobEngine.Application.records.job;
using JobEngine.Application.services.job;
using JobEngine.Application.services.retry;
using JobEngine.Domain.models;
using Microsoft.EntityFrameworkCore;

namespace JobEngine.Worker;

public class WorkerService(IServiceScopeFactory scopes, ILogger<WorkerService> logger, IConfiguration config, IJobQueue queue, RetryPolicy retryPolicy) : BackgroundService
{
    private readonly string _workerId = Guid.NewGuid().ToString();
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var concurrency = config.GetValue("Worker:Concurrency", 3);
        logger.LogInformation("Worker host up with {Concurrency} consume loops.", concurrency);

        var loops = Enumerable.Range(0, concurrency).Select(i => LoopAsync(i, stoppingToken));
        await Task.WhenAll(loops);

        logger.LogInformation("Worker host stopping.");
    }
    
    private async Task LoopAsync(int loopId, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                var jobId = await queue.DequeueAsync(_workerId, stop);
                if (jobId is null) continue;
                
                // one dbcontext per job
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IJobDbContext>();

                var job = await ClaimAsync(db, jobId.Value, CancellationToken.None);
                if (job is null)
                {
                    await queue.AcknowledgeAsync(_workerId, jobId.Value);
                    continue;
                }

                await RunAsync(job, scope.ServiceProvider, db, loopId, stop);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Loop {Loop} error", loopId);
                await DelayAsync(1000, stop);        
            }
        }
    }

    private static async Task<Job?> ClaimAsync(IJobDbContext db, Guid jobId, CancellationToken ct)
    {
        var jobs = await db.Jobs.FromSqlInterpolated($@"
            UPDATE jobs
            SET status = 'Running', attempt_count = attempt_count + 1, lease_token = gen_random_uuid()
            WHERE id = {jobId} AND status IN ('Pending', 'Scheduled', 'Retrying')
            RETURNING *").AsNoTracking().ToListAsync(ct);

        return jobs.FirstOrDefault();
    }
    
    private async Task RunAsync(Job job, IServiceProvider sp, IJobDbContext db, int loopId, CancellationToken stop)
    {
        logger.LogInformation("[Loop {Loop}] Job {Id} ({Type}) started, attempt {Attempt}", loopId, job.Id, job.Type, job.AttemptCount);

        var attempt = new JobAttempt
        {
            JobId = job.Id,
            AttemptNumber = job.AttemptCount,
            StartedAt = DateTimeOffset.UtcNow
        };
        
        db.JobAttempts.Add(attempt);
        await db.SaveChangesAsync(CancellationToken.None);
        
        // create a new ct which makes the cancellation only by the timeout
        // so shutdown waits for the current running jobs to finish
        var timeout = TimeSpan.FromSeconds(config.GetValue("Worker:JobTimeoutSeconds", 600));
        using var jobCts = new CancellationTokenSource(timeout);
        
        string? error = null;
        bool nonRetryable = false;
        try
        {
            var handler = sp.GetServices<IJobHandler>().FirstOrDefault(h => h.JobType == job.Type) ?? throw new NonRetryableException($"No handler for job type '{job.Type}'");

            await handler.HandleAsync(new JobContext(job.Id, job.Payload, job.AttemptCount), jobCts.Token);
        }
        catch (OperationCanceledException) when (jobCts.IsCancellationRequested)
        {
            error = $"Job timed out after {timeout.TotalSeconds:0}s";
        }
        catch (Exception ex)
        {
            error = ex.Message.Length > 4000 ? ex.Message[..4000] : ex.Message;
            nonRetryable = ex is NonRetryableException;
        }
        
        attempt.FinishedAt = DateTimeOffset.UtcNow;
        attempt.Result = error is null ? JobAttemptResult.Success : JobAttemptResult.Failed;
        attempt.Error = error;
        await db.SaveChangesAsync(CancellationToken.None);

        string outcome;
        int rows;

        if (error is null)
        {
            outcome = "Completed";
            rows = await db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE jobs SET status = 'Completed', completed_at = now()
            WHERE id = {job.Id} AND lease_token = {job.LeaseToken} AND status = 'Running'", CancellationToken.None);
        }
        else if (!nonRetryable && job.AttemptCount < job.MaxAttempts)
        {
            outcome = "Retrying";
            var nextRunAt = DateTimeOffset.UtcNow + retryPolicy.GetDelay(job.AttemptCount);

            rows = await db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE jobs SET status = 'Retrying', last_error = {error}, run_at = {nextRunAt}
            WHERE id = {job.Id} AND lease_token = {job.LeaseToken} AND status = 'Running'", CancellationToken.None);

            if (rows == 1)
            {
                try { await queue.ScheduleAsync(job.Id, job.Priority, nextRunAt); }
                catch (Exception ex) { logger.LogWarning(ex, "Job {Id}: could not schedule retry in Redis; Reconciler will pick it up", job.Id); }
            }
        }
        else
        {
            outcome = "Dead";
            rows = await db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE jobs SET status = 'Dead', last_error = {error}
            WHERE id = {job.Id} AND lease_token = {job.LeaseToken} AND status = 'Running'", CancellationToken.None);

            if (rows == 1)
            {
                try { await queue.PushToDeadLetterAsync(job.Id); }
                catch (Exception ex) { logger.LogWarning(ex, "Job {Id}: could not push to dlq", job.Id); }
            }
        }
        
        
        if (rows == 0)
            logger.LogWarning("Job {Id}: lost the job, result not saved", job.Id);
        else
            logger.LogInformation("[Loop {Loop}] Job {Id} finished: {Outcome}", loopId, job.Id, outcome);

        await queue.AcknowledgeAsync(_workerId, job.Id);
    }

    
    private static async Task DelayAsync(int ms, CancellationToken ct)
    {
        try { await Task.Delay(ms, ct); }
        catch (OperationCanceledException) { }
    }
}
