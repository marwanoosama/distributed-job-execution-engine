namespace JobEngine.Domain.models;

public enum JobAttemptResult
{
    Success = 0,
    Failed = 1,
    LeaseExpired = 2,
}
