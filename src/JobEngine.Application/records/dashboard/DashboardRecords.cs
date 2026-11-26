namespace JobEngine.Application.records.dashboard;

public record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);

public record JobAttemptResponse(
    int AttemptNumber, Guid? WorkerId, string? WorkerHostname,
    DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, string? Result, string? Error);

public record WorkerResponse(
    Guid Id, string Hostname, string Status, int Concurrency,
    DateTimeOffset StartedAt, DateTimeOffset LastHeartbeat, int RunningJobs);

public class MinuteCount
{
    public DateTimeOffset Minute { get; set; }
    public int Count { get; set; }
}

public record StatsResponse(
    Dictionary<string, int> JobsByStatus,
    int CompletedLastHour,
    double AttemptFailureRatePercent,
    List<MinuteCount> CompletedPerMinute);