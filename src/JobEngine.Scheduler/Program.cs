using JobEngine.Application;
using JobEngine.Infrastructure;
using JobEngine.Scheduler;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddJobEnginePersistence(builder.Configuration.GetConnectionString("Postgres")!,
    builder.Configuration.GetConnectionString("Redis")!);

builder.Services.AddHostedService<DelayedJobMover>();
builder.Services.AddHostedService<Reconciler>();


builder.Services.AddRetryPolicy(builder.Configuration); 
builder.Services.AddHostedService<CrashMonitor>();

var host = builder.Build();
host.Run();
