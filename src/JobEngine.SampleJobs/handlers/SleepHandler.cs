using System.Text.Json;
using JobEngine.Application.interfaces;
using JobEngine.Application.records;
using JobEngine.Application.records.job;
using Microsoft.Extensions.Logging;

namespace JobEngine.SampleJobs.handlers;

public class SleepHandler(ILogger<SleepHandler> log) : IJobHandler
{
    public string JobType => "Sleep";
    
    public async Task HandleAsync(JobContext context, CancellationToken ct)
    {
        var seconds = JsonDocument.Parse(context.PayloadJson).RootElement.GetProperty("seconds").GetInt32();
        log.LogInformation("Job {Id} sleeping {S}s", context.JobId, seconds);
        await Task.Delay(TimeSpan.FromSeconds(seconds), ct);        
    }
}