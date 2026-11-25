using JobEngine.Application.records.job;
using JobEngine.Application.services.job;

namespace JobEngine.Api.endpoints;

public static class JobEndpoints
{
    public static void MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/jobs", async (SubmitJobRequest request, JobService jobService, CancellationToken ct) =>
        {
            var errors = request.Validate();
            if (errors.Count > 0)
                return Results.BadRequest(new { errors });

            var res = await jobService.CreateJob(request, ct);
            return Results.Accepted($"/api/jobs/{res.Id}", new { jobId = res.Id });
        });

        app.MapGet("/api/jobs/{id:guid}", async (Guid id, JobService jobService, CancellationToken ct) =>
        {
            var res = await jobService.GetJob(id, ct);
            return res is null
                ? Results.NotFound(new { error = "job not found" })
                : Results.Ok(JobResponse.From(res));
        });
        
        app.MapPost("/api/jobs/{id:guid}/retry", async (Guid id, JobService jobService, CancellationToken ct) =>
            await jobService.RetryJob(id, ct) ? Results.Accepted($"/api/jobs/{id}") : Results.Conflict(new { error = "job not found or not Dead" }));
        
        app.MapPost("/api/jobs/{id:guid}/cancel", async (Guid id, JobService jobService, CancellationToken ct) =>
            await jobService.CancelJob(id, ct) ? Results.Ok(new { status = "Cancelled" }) : Results.Conflict(new { error = "job not found, already running, or finished" }));
    }
}
