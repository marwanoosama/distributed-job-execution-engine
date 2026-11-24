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
            RunAt = DateTimeOffset.UtcNow
        };
        
        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync(ct);

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
}
