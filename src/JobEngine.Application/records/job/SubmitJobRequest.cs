using System.Text.Json;

namespace JobEngine.Application.records.job;

public record SubmitJobRequest(string Type,
    JsonElement Payload,
    int Priority = 1,
    int? MaxAttempts = null,
    string? IdempotencyKey = null,
    DateTimeOffset? RunAt = null)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Type))
            errors.Add("type is required");
        else if (Type.Trim().Length > 200)
            errors.Add("type must be at most 200 characters");

        if (Priority is < 0 or > 2)
            errors.Add("priority must be 0 (high), 1 (normal) or 2 (low)");

        if (MaxAttempts is < 1 or > 100)
            errors.Add("maxAttempts must be between 1 and 100");
        
        if (IdempotencyKey is { Length: > 200 })
            errors.Add("idempotencyKey must be at most 200 characters");
        
        if (RunAt is not null && RunAt > DateTimeOffset.UtcNow.AddYears(1))
            errors.Add("runAt must be within one year");

        return errors;
    }
}
