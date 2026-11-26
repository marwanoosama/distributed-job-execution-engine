using JobEngine.Application.services.dashboard;
using JobEngine.Domain.models;

namespace JobEngine.Api.endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/jobs", async (string? status, string? type, int? priority, int? page, int? pageSize,
            DashboardService dashboard, CancellationToken ct) =>
        {
            JobStatus? parsed = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<JobStatus>(status, ignoreCase: true, out var s))
                    return Results.BadRequest(new { error = $"unknown status '{status}'" });
                parsed = s;
            }

            var p = Math.Max(page ?? 1, 1);
            var size = Math.Clamp(pageSize ?? 20, 1, 100);
            return Results.Ok(await dashboard.ListJobs(parsed, type, priority, p, size, ct));
        });

        app.MapGet("/api/jobs/{id:guid}/attempts", async (Guid id, DashboardService dashboard, CancellationToken ct) =>
            Results.Ok(await dashboard.GetAttempts(id, ct)));

        app.MapGet("/api/workers", async (DashboardService dashboard, CancellationToken ct) =>
            Results.Ok(await dashboard.ListWorkers(ct)));

        app.MapGet("/api/stats", async (DashboardService dashboard, CancellationToken ct) =>
            Results.Ok(await dashboard.GetStats(ct)));
    }
}