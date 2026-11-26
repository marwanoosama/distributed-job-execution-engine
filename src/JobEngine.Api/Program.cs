using JobEngine.Api.endpoints;
using JobEngine.Application.services.dashboard;
using JobEngine.Application.services.job;
using JobEngine.Infrastructure;
using JobEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddJobEnginePersistence(builder.Configuration.GetConnectionString("Postgres")!,
    builder.Configuration.GetConnectionString("Redis")!);

builder.Services.AddScoped<JobService>();
builder.Services.AddScoped<DashboardService>(); 

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:3000").AllowAnyHeader().AllowAnyMethod()));


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<JobDbContext>();
    db.Database.Migrate();
}
app.UseCors();

app.MapJobEndpoints();
app.MapDashboardEndpoints();
app.Run();
