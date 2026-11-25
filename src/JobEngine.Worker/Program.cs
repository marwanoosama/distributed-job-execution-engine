using JobEngine.Application;
using JobEngine.Application.interfaces;
using JobEngine.Infrastructure;
using JobEngine.SampleJobs.handlers;
using JobEngine.Worker;
using WorkerEntity = JobEngine.Domain.models.Worker;

var builder = Host.CreateApplicationBuilder(args);

// after shutdown, wait at most "ShutdownTimeoutSeconds" seconds
builder.Services.Configure<HostOptions>(o =>
    o.ShutdownTimeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Worker:ShutdownTimeoutSeconds", 30)));

// one worker identity for the whole process, shared by WorkerService and WorkerHeartbeatService
builder.Services.AddSingleton(new WorkerEntity { Hostname = Environment.MachineName });

builder.Services.AddHostedService<WorkerHeartbeatService>();
builder.Services.AddHostedService<WorkerService>();

builder.Services.AddRetryPolicy(builder.Configuration);

builder.Services.AddJobEnginePersistence(builder.Configuration.GetConnectionString("Postgres")!,
    builder.Configuration.GetConnectionString("Redis")!);

builder.Services.AddScoped<IJobHandler, SleepHandler>();
builder.Services.AddScoped<IJobHandler, AlwaysFailHandler>();
builder.Services.AddScoped<IJobHandler, SendEmailHandler>();
builder.Services.AddScoped<IJobHandler, FailRandomlyHandler>();

var host = builder.Build();
host.Run();