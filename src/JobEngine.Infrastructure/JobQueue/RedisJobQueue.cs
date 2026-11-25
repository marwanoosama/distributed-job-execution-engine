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

    #region redis keys

    private static string ProcessingKey(string workerId) => $"processing:{workerId}";
    private const string DelayedKey = "delayed";
    private const string DeadLetterKey = "dlq";

    #endregion
    
    // lus script that promotes jobs from the delayed sorted set into their corresponding priority queues (queue:high, queue:normal, ...) 
    private const string MoveDueScript = @"
        -- from the delayed set, get 100 jobs whose score <= current time
        local members = redis.call('ZRANGEBYSCORE', KEYS[1], '-inf', ARGV[1], 'LIMIT', 0, 100)

        -- loops through each job (0:jobA, 1:jobB, ..), removes it from delayed set, extract the first char (which maps to priority level), strips the first 2 chars (0:, 1:, ...) to get the job id, then push it to it's queue
        for _, m in ipairs(members) do
            redis.call('ZREM', KEYS[1], m)
            local queue = 'queue:normal'
            local p = string.sub(m, 1, 1)
            if p == '0' then queue = 'queue:high' elseif p == '2' then queue = 'queue:low' end
            redis.call('LPUSH', queue, string.sub(m, 3))
        end
        return members";
    
    
    
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
    
    // add scheduled job to delayed queue
    public Task ScheduleAsync(Guid jobId, int priority, DateTimeOffset runAt)
    {
        return Redis.SortedSetAddAsync(DelayedKey, $"{priority}:{jobId}", runAt.ToUnixTimeMilliseconds());
    }
    
    // move scheduled jobs from delayed to their queue
    public async Task<int> MoveDueJobsAsync(DateTimeOffset now)
    {
        var result = await Redis.ScriptEvaluateAsync(
            MoveDueScript,
            new RedisKey[] { DelayedKey },
            new RedisValue[] { now.ToUnixTimeMilliseconds() });
        
        return ((RedisResult[])result!).Length;
    }

    public Task PushToDeadLetterAsync(Guid jobId)
    {
        return Redis.ListLeftPushAsync(DeadLetterKey, jobId.ToString());
    }

    public Task RemoveFromDeadLetterAsync(Guid jobId)
    {
        return Redis.ListRemoveAsync(DeadLetterKey, jobId.ToString(), 1);
    }
}