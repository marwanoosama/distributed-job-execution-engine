using JobEngine.Application.interfaces;
using JobEngine.Application.records;
using JobEngine.Application.records.job;
using Microsoft.Extensions.Logging;

namespace JobEngine.SampleJobs.handlers;

public class AlwaysFailHandler(ILogger<AlwaysFailHandler> log) : IJobHandler
{
    public string JobType => "AlwaysFail";
    
    public Task HandleAsync(JobContext ctx, CancellationToken ct) => 
        throw new InvalidOperationException("I always fail");
}