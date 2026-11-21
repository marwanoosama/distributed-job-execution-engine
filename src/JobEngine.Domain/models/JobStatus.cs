namespace JobEngine.Domain.models;

public enum JobStatus
{
    Scheduled = 0,
    Pending = 1,
    Running = 2,
    Retrying = 3,
    Completed = 4,
    Dead = 5,
    Cancelled = 6,
}
