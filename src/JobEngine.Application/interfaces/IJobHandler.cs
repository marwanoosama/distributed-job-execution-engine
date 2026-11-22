using JobEngine.Application.records;
using JobEngine.Application.records.job;

namespace JobEngine.Application.interfaces;

public interface IJobHandler
{
    string JobType { get; }
    Task HandleAsync(JobContext context, CancellationToken ct);
}