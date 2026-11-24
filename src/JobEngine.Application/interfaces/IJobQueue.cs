namespace JobEngine.Application.interfaces;

public interface IJobQueue
{
    Task EnqueueAsync(Guid jobId, int priority);
    Task<Guid?> DequeueAsync(string workerId, CancellationToken ct);
    Task AcknowledgeAsync(string workerId, Guid jobId);
}