using JobEngine.Application.interfaces;
using JobEngine.Application.records;
using JobEngine.Application.records.job;

namespace JobEngine.SampleJobs.handlers;

public class FailRandomlyHandler : IJobHandler
{
    public string JobType => "FailRandomly";

    public async Task HandleAsync(JobContext ctx, CancellationToken ct)
    {
        await Task.Delay(500, ct);
        if (Random.Shared.NextDouble() < 0.3)
        {
            throw new InvalidOperationException("Random failure");
        }
    }
}