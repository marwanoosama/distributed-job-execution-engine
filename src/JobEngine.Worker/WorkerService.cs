using JobEngine.Application.interfaces;
using JobEngine.Application.records.job;
using JobEngine.Application.services.job;
using JobEngine.Application.services.retry;
using JobEngine.Domain.models;
using Microsoft.EntityFrameworkCore;
using WorkerEntity = JobEngine.Domain.models.Worker;

namespace JobEngine.Worker;

public class WorkerService(IServiceScopeFactory scopes, ILogger<WorkerService> logger, IConfiguration config,
    IJobQueue queue, RetryPolicy retryPolicy, ILeaseManager leaseManager, WorkerEntity me) : BackgroundService
{
    private string WorkerKey => me.Id.ToString();
    private TimeSpan LeaseTtl => TimeSpan.FromSeconds(config.GetValue("Worker:LeaseSeconds", 30));
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var concurrency = config.GetValue("Worker:Concurrency", 3);
        // save the current worker in db
        await RegisterWorkerAsync(concurrency);
        logger.LogInformation("Worker {Id} (pid {Pid}) up with {Concurrency} consume loops.", me.Id, Environment.ProcessId, concurrency);

        var loops = Enumerable.Range(0, concurrency).Select(i => LoopAsync(i, stoppingToken));
        await Task.WhenAll(loops);

        await MarkStoppedAsync();
        logger.LogInformation("Worker host stopping.");
    }
    
    private async Task RegisterWorkerAsync(int concurrency)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IJobDbContext>();
        var now = DateTimeOffset.UtcNow;
        db.Workers.Add(new WorkerEntity
        {
            Id = me.Id,
            Hostname = me.Hostname,
            Concurrency = concurrency,
            Status = WorkerStatus.Alive,
            StartedAt = now,
            LastHeartbeat = now
        });
        await db.SaveChangesAsync(CancellationToken.None);
    }
    
    private async Task MarkStoppedAsync()
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IJobDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE workers SET status = 'Stopped' WHERE id = {me.Id}", CancellationToken.None);
        }
        catch (Exception ex) { logger.LogWarning(ex, "Could not mark worker as stopped"); }
    }
    
    private async Task LoopAsync(int loopId, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                var jobId = await queue.DequeueAsync(WorkerKey, stop);
                if (jobId is null) continue;
                
                // try to acquire a lease, if there is already a lease cached, that means the job is being processed by another worker so continue with the next one
                var leaseToken = Guid.NewGuid();
                if (!await leaseManager.TryAcquireAsync(jobId.Value, leaseToken, LeaseTtl))
                {
                    await queue.AcknowledgeAsync(WorkerKey, jobId.Value);  
                    continue;
                }
                
                // one dbcontext per job
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IJobDbContext>();

                // take the job
                var job = await ClaimAsync(db, jobId.Value, leaseToken, CancellationToken.None);
                if (job is null)
                {
                    await leaseManager.ReleaseAsync(jobId.Value, leaseToken);
                    await queue.AcknowledgeAsync(WorkerKey, jobId.Value);
                    continue;
                }

                await RunAsync(job, leaseToken, scope.ServiceProvider, db, loopId);
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

    private static async Task<Job?> ClaimAsync(IJobDbContext db, Guid jobId, Guid leaseToken, CancellationToken ct)
    {
        var jobs = await db.Jobs.FromSqlInterpolated($@"
            UPDATE jobs
            SET status = 'Running', attempt_count = attempt_count + 1, lease_token = {leaseToken}
            WHERE id = {jobId} AND status IN ('Pending', 'Scheduled', 'Retrying')
            RETURNING *").AsNoTracking().ToListAsync(ct);

        return jobs.FirstOrDefault();
    }
    
    private async Task RunAsync(Job job, Guid leaseToken, IServiceProvider sp, IJobDbContext db, int loopId)
    {
        logger.LogInformation("[Loop {Loop}] Job {Id} ({Type}) started, attempt {Attempt}", loopId, job.Id, job.Type, job.AttemptCount);

        var attempt = new JobAttempt
        {
            JobId = job.Id,
            WorkerId = me.Id,
            AttemptNumber = job.AttemptCount,
            StartedAt = DateTimeOffset.UtcNow
        };
        
        db.JobAttempts.Add(attempt);
        await db.SaveChangesAsync(CancellationToken.None);
        
        // create a new ct which makes the cancellation only by the timeout
        // so shutdown waits for the current running jobs to finish
        var timeout = TimeSpan.FromSeconds(config.GetValue("Worker:JobTimeoutSeconds", 600));
        using var jobCts = new CancellationTokenSource(timeout);

        var leaseLost = false;
        using var renewCts = new CancellationTokenSource();
        
        // keep the lease alive while the job runs. if the lease is lost, stop renewing the lease key
        var renewTask = RenewLeaseLoopAsync(job.Id, leaseToken, () => { leaseLost = true; jobCts.Cancel(); }, renewCts.Token);
        
        string? error = null;
        bool nonRetryable = false;
        try
        {
            var handler = sp.GetServices<IJobHandler>().FirstOrDefault(h => h.JobType == job.Type) ?? throw new NonRetryableException($"No handler for job type '{job.Type}'");

            await handler.HandleAsync(new JobContext(job.Id, job.Payload, job.AttemptCount), jobCts.Token);
        }
        catch (OperationCanceledException) when (jobCts.IsCancellationRequested)
        {
            error = leaseLost ? "Lost the lease" : $"Job timed out after {timeout.TotalSeconds:0}s";
        }
        catch (Exception ex)
        {
            error = ex.Message.Length > 4000 ? ex.Message[..4000] : ex.Message;
            nonRetryable = ex is NonRetryableException;
        }
        
        renewCts.Cancel();
        await renewTask;
        
        if (leaseLost)
        {
            logger.LogWarning("Job {Id}: lease lost, result discarded", job.Id);
            await queue.AcknowledgeAsync(WorkerKey, job.Id);
            return;
        }
        
        var attemptResult = error is null ? "Success" : "Failed";
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE job_attempts SET finished_at = now(), result = {attemptResult}, error = {error}
            WHERE id = {attempt.Id} AND finished_at IS NULL", CancellationToken.None);

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

        // release the lease only after the job status is saved
        await leaseManager.ReleaseAsync(job.Id, leaseToken);
        await queue.AcknowledgeAsync(WorkerKey, job.Id);
    }

    // renews the lease every few seconds while the job runs. calls onLost() if the lease is no longer mine
    private async Task RenewLeaseLoopAsync(Guid jobId, Guid leaseToken, Action onLost, CancellationToken stop)
    {
        var every = TimeSpan.FromSeconds(config.GetValue("Worker:LeaseRenewSeconds", 10));
        using var timer = new PeriodicTimer(every);
        try
        {
            while (await timer.WaitForNextTickAsync(stop))
            {
                try
                {
                    if (await leaseManager.RenewAsync(jobId, leaseToken, LeaseTtl)) continue;

                    logger.LogWarning("Job {Id}: lease lost", jobId);
                    onLost();
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Job {Id}: lease renew failed, will try again", jobId);
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    
    private static async Task DelayAsync(int ms, CancellationToken ct)
    {
        try { await Task.Delay(ms, ct); }
        catch (OperationCanceledException) { }
    }
}
