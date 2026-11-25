using JobEngine.Api.endpoints;
using JobEngine.Application;
using JobEngine.Application.services.job;
using JobEngine.Infrastructure;
using JobEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddJobEnginePersistence(builder.Configuration.GetConnectionString("Postgres")!,
    builder.Configuration.GetConnectionString("Redis")!);

builder.Services.AddRetryPolicy(builder.Configuration);

builder.Services.AddScoped<JobService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<JobDbContext>();
    db.Database.Migrate();
}

app.MapJobEndpoints();

app.Run();
