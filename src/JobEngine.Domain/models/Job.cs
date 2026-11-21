using JobEngine.Domain.models;

namespace JobEngine.Domain;

public class Job
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = "";
    public string Payload { get; set; } = "{}";
    public int Priority { get; set; } = 1;
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public string? IdempotencyKey { get; set; }
    public DateTimeOffset RunAt { get; set; }
    public Guid? LeaseToken { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}
