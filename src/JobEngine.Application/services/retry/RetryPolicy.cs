using Microsoft.Extensions.Options;

namespace JobEngine.Application.services.retry;

public class RetryPolicy(IOptions<RetryPolicyOptions> options)
{
    public TimeSpan GetDelay(int attempt)
    {
        var o = options.Value;
        var seconds = Math.Min(o.MaxDelaySeconds, o.BaseDelaySeconds * Math.Pow(2, attempt - 1));
        return TimeSpan.FromSeconds(seconds + Random.Shared.NextDouble() * o.MaxJitterSeconds);
    }
}
