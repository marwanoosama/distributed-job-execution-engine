namespace JobEngine.Domain.models;

public class Worker
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Hostname { get; set; } = "";
    public int Concurrency { get; set; } = 1;
    public WorkerStatus Status { get; set; } = WorkerStatus.Alive;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset LastHeartbeat { get; set; }
}
