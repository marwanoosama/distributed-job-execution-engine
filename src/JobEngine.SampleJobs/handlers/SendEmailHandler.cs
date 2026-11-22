using JobEngine.Application.interfaces;
using JobEngine.Application.records;
using JobEngine.Application.records.job;
using Microsoft.Extensions.Logging;

namespace JobEngine.SampleJobs.handlers;

public class SendEmailHandler(ILogger<SendEmailHandler> log) : IJobHandler
{
    public string JobType => "SendEmail";

    public async Task HandleAsync(JobContext ctx, CancellationToken ct)
    {
        await Task.Delay(1000, ct); // sending an email
        log.LogInformation("Email sent for job {Id}", ctx.JobId);
    }
}