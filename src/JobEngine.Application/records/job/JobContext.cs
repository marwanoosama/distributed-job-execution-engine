namespace JobEngine.Application.records.job;

public record JobContext(Guid JobId, string PayloadJson, int Attempt);
