namespace JobEngine.Application.interfaces;

public interface IJobQueue
{
    Task EnqueueAsync(Guid jobId, int priority);
    Task<Guid?> DequeueAsync(string workerId, CancellationToken ct);
    Task AcknowledgeAsync(string workerId, Guid jobId);
    Task ScheduleAsync(Guid jobId, int priority, DateTimeOffset runAt);
    Task<int> MoveDueJobsAsync(DateTimeOffset now);
    Task PushToDeadLetterAsync(Guid jobId);
    Task RemoveFromDeadLetterAsync(Guid jobId);
}