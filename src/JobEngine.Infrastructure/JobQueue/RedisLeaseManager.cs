using JobEngine.Application.interfaces;
using StackExchange.Redis;

namespace JobEngine.Infrastructure.JobQueue;

public class RedisLeaseManager(IConnectionMultiplexer mux) : ILeaseManager
{
    private IDatabase Redis => mux.GetDatabase();
    private static string LeaseKey(Guid jobId) => $"lease:{jobId}";
    
    private const string RenewScript = @"
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            return redis.call('PEXPIRE', KEYS[1], ARGV[2])
        end
        return 0";
    
    private const string ReleaseScript = @"
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            return redis.call('DEL', KEYS[1])
        end
        return 0";
    
    public Task<bool> TryAcquireAsync(Guid jobId, Guid token, TimeSpan ttl)
    {
        return Redis.StringSetAsync(LeaseKey(jobId), token.ToString(), ttl, When.NotExists);
    }
    
    public async Task<bool> RenewAsync(Guid jobId, Guid token, TimeSpan ttl)
    {
        var result = await Redis.ScriptEvaluateAsync(RenewScript,
            new RedisKey[] { LeaseKey(jobId) },
            new RedisValue[] { token.ToString(), (long)ttl.TotalMilliseconds });
        
        return (long)result == 1;
    }

    public async Task ReleaseAsync(Guid jobId, Guid token)
    {
        await Redis.ScriptEvaluateAsync(ReleaseScript,
            new RedisKey[] { LeaseKey(jobId) },
            new RedisValue[] { token.ToString() });
    }

    public Task<bool> ExistsAsync(Guid jobId)
    {
        return Redis.KeyExistsAsync(LeaseKey(jobId));
    }
}