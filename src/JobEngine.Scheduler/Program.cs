using JobEngine.Infrastructure;
using JobEngine.Scheduler;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddJobEnginePersistence(builder.Configuration.GetConnectionString("Postgres")!,
    builder.Configuration.GetConnectionString("Redis")!);

builder.Services.AddHostedService<SchedulerService>();
builder.Services.AddHostedService<Reconciler>();

var host = builder.Build();
host.Run();
