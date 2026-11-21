namespace JobEngine.Domain.models;

public class JobAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;

    public Guid? WorkerId { get; set; }
    public Worker? Worker { get; set; }

    public int AttemptNumber { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public JobAttemptResult? Result { get; set; }
    public string? Error { get; set; }
}
