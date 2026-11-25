namespace JobEngine.Application.services.retry;

public class RetryPolicyOptions
{
    public const string SectionName = "RetryPolicy";

    public double BaseDelaySeconds { get; set; } = 5;
    public double MaxDelaySeconds { get; set; } = 300;
    public double MaxJitterSeconds { get; set; } = 3;
}
