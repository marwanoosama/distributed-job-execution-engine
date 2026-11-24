using JobEngine.Application.interfaces;
using StackExchange.Redis;

namespace JobEngine.Infrastructure.JobQueue;

public class RedisJobQueue(IConnectionMultiplexer mux) : IJobQueue
{
    private IDatabase Redis => mux.GetDatabase();

    private static string QueueKey(int priority) => priority switch
    {
        0 => "queue:high",
        1 => "queue:normal",
        _ => "queue:low"
    };

    private static string ProcessingKey(string workerId) => $"processing:{workerId}";
    
    // add a new job to queue list
    public Task EnqueueAsync(Guid jobId, int priority)
    {
        return Redis.ListLeftPushAsync(QueueKey(priority), jobId.ToString());
    }

    // pop from queue list and insert in worker's processing list
    public async Task<Guid?> DequeueAsync(string workerId, CancellationToken ct)
    {
        var processingKey = ProcessingKey(workerId);
        foreach (var priority in new[] { 0, 1, 2 }) 
        {
            var value = await Redis.ListMoveAsync(QueueKey(priority), processingKey, ListSide.Right, ListSide.Left);
            if (value.IsNull) continue;

            if (Guid.TryParse(value.ToString(), out var id))
                return id;

            await Redis.ListRemoveAsync(processingKey, value, 1);
        }

        await Task.Delay(200, ct);
        return null;
    }

    public Task AcknowledgeAsync(string workerId, Guid jobId)
    {
        return Redis.ListRemoveAsync(ProcessingKey(workerId), jobId.ToString(), 1);
    }
}