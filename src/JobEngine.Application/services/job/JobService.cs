using JobEngine.Application.interfaces;
using JobEngine.Application.records.job;
using JobEngine.Domain.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace JobEngine.Application.services.job;

public class JobService(IJobDbContext dbContext, IJobQueue queue, ILogger<JobService> logger)
{
    public async Task<Job> CreateJob(SubmitJobRequest req, CancellationToken ct = default)
    {
        var idempotencyKey = string.IsNullOrWhiteSpace(req.IdempotencyKey) ? null : req.IdempotencyKey.Trim();
        
        if (idempotencyKey is not null)
        {
            var existing = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.IdempotencyKey == idempotencyKey, ct);
            if (existing is not null) return existing;   
        }
        
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = req.Type.Trim(),
            Payload = req.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                ? "{}"
                : req.Payload.GetRawText(),
            Priority = req.Priority,
            MaxAttempts = req.MaxAttempts ?? 5,
            Status = JobStatus.Pending,
            RunAt = DateTimeOffset.UtcNow,
            IdempotencyKey = idempotencyKey
        };

        try
        {
            dbContext.Jobs.Add(job);
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (idempotencyKey is not null)
        {
            dbContext.Entry(job).State = EntityState.Detached;
            var existing = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.IdempotencyKey == idempotencyKey, ct);
            if (existing is null) throw;
            return existing;
        }

        try
        {
            await queue.EnqueueAsync(job.Id, job.Priority);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Job {JobId} saved to Postgres but could not be enqueued in Redis; Reconciler will pick it up", job.Id);
        }

        return job;
    }

    public async Task<Job?> GetJob(Guid id, CancellationToken ct = default)
    {
        var job = await  dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return job;
    }
    
    public async Task<bool> RetryJob(Guid id, CancellationToken ct = default)
    {
        var rows = await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
        UPDATE jobs
        SET status = 'Pending', attempt_count = 0, last_error = NULL, lease_token = NULL,
            completed_at = NULL, run_at = now()
        WHERE id = {id} AND status = 'Dead'", ct);
        if (rows == 0) return false;

        var job = await GetJob(id, ct);
        await queue.RemoveFromDeadLetterAsync(id);
        await queue.EnqueueAsync(id, job!.Priority);
        return true;
    }
}
