using System.Text.Json;
using JobEngine.Domain.models;

namespace JobEngine.Application.records.job;

public record JobResponse(
    Guid JobId,
    string Type,
    JsonElement Payload,
    int Priority,
    string Status,
    int AttemptCount,
    int MaxAttempts,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset RunAt)
{
    public static JobResponse From(Job job) => new(
        job.Id,
        job.Type,
        ParsePayload(job.Payload),
        job.Priority,
        job.Status.ToString(),
        job.AttemptCount,
        job.MaxAttempts,
        job.LastError,
        job.CreatedAt,
        job.CompletedAt,
        job.RunAt);

    static JsonElement ParsePayload(string payload)
    {
        try
        {
            return JsonDocument.Parse(payload).RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}").RootElement.Clone();
        }
    }
}
