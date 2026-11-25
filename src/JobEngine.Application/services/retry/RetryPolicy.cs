namespace JobEngine.Application.services.retry;

public static class RetryPolicy
{
    private static RetryPolicyOptions _options = new();

    public static void Configure(RetryPolicyOptions options) => _options = options;

    public static TimeSpan GetDelay(int attempt)
    {
        var seconds = Math.Min(_options.MaxDelaySeconds, _options.BaseDelaySeconds * Math.Pow(2, attempt - 1));
        var jitter = Random.Shared.NextDouble() * _options.MaxJitterSeconds;
        return TimeSpan.FromSeconds(seconds + jitter);
    }
}
