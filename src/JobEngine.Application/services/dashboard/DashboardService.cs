using JobEngine.Application.interfaces;
using JobEngine.Application.records.dashboard;
using JobEngine.Application.records.job;
using JobEngine.Domain.models;
using Microsoft.EntityFrameworkCore;

namespace JobEngine.Application.services.dashboard;

public class DashboardService(IJobDbContext db)
{
    public async Task<PagedResult<JobResponse>> ListJobs(JobStatus? status, string? type, int? priority,
        int page, int pageSize, CancellationToken ct)
    {
        var query = db.Jobs.AsNoTracking();
        if (status is not null) query = query.Where(j => j.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(j => j.Type == type);
        if (priority is not null) query = query.Where(j => j.Priority == priority.Value);

        var total = await query.CountAsync(ct);
        var jobs = await query.OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<JobResponse>(jobs.Select(JobResponse.From).ToList(), total, page, pageSize);
    }

    public async Task<List<JobAttemptResponse>> GetAttempts(Guid jobId, CancellationToken ct)
    {
        var attempts = await db.JobAttempts.AsNoTracking().Include(a => a.Worker)
            .Where(a => a.JobId == jobId)
            .OrderBy(a => a.AttemptNumber).ThenBy(a => a.StartedAt)
            .ToListAsync(ct);

        return attempts.Select(a => new JobAttemptResponse(
            a.AttemptNumber, a.WorkerId, a.Worker?.Hostname,
            a.StartedAt, a.FinishedAt, a.Result?.ToString(), a.Error)).ToList();
    }

    public async Task<List<WorkerResponse>> ListWorkers(CancellationToken ct)
    {
        var workers = await db.Workers.AsNoTracking()
            .OrderBy(w => w.Status == WorkerStatus.Alive ? 0 : 1)
            .ThenByDescending(w => w.LastHeartbeat)
            .Take(50).ToListAsync(ct);

        var running = await db.JobAttempts.AsNoTracking()
            .Where(a => a.FinishedAt == null && a.WorkerId != null)
            .GroupBy(a => a.WorkerId!.Value)
            .Select(g => new { WorkerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.WorkerId, x => x.Count, ct);

        return workers.Select(w => new WorkerResponse(
            w.Id, w.Hostname, w.Status.ToString(), w.Concurrency, w.StartedAt, w.LastHeartbeat,
            running.GetValueOrDefault(w.Id))).ToList();
    }

    public async Task<StatsResponse> GetStats(CancellationToken ct)
    {
        var counts = await db.Jobs.AsNoTracking()
            .GroupBy(j => j.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byStatus = Enum.GetValues<JobStatus>()
            .ToDictionary(s => s.ToString(), s => counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0);

        var since = DateTimeOffset.UtcNow.AddHours(-1);

        var attempts = await db.JobAttempts.AsNoTracking()
            .Where(a => a.FinishedAt >= since)
            .GroupBy(a => a.Result)
            .Select(g => new { Result = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var totalAttempts = attempts.Sum(a => a.Count);
        var okAttempts = attempts.Where(a => a.Result == JobAttemptResult.Success).Sum(a => a.Count);
        var failureRate = totalAttempts == 0 ? 0 : Math.Round((totalAttempts - okAttempts) * 100.0 / totalAttempts, 1);

        var rows = await db.Database.SqlQuery<MinuteCount>($@"
            SELECT date_trunc('minute', completed_at) AS ""Minute"", count(*)::int AS ""Count""
            FROM jobs WHERE completed_at >= {since}
            GROUP BY 1 ORDER BY 1").ToListAsync(ct);

        var start = new DateTimeOffset(since.Year, since.Month, since.Day, since.Hour, since.Minute, 0, TimeSpan.Zero);
        var perMinute = Enumerable.Range(0, 61).Select(i => start.AddMinutes(i))
            .Select(m => new MinuteCount
            {
                Minute = m,
                Count = rows.FirstOrDefault(r => r.Minute.UtcDateTime == m.UtcDateTime)?.Count ?? 0
            }).ToList();

        return new StatsResponse(byStatus, perMinute.Sum(p => p.Count), failureRate, perMinute);
    }
}