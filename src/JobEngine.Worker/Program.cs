using JobEngine.Application;
using JobEngine.Application.interfaces;
using JobEngine.Infrastructure;
using JobEngine.SampleJobs.handlers;
using JobEngine.Worker;

var builder = Host.CreateApplicationBuilder(args);

// after shutdown, wait at most "ShutdownTimeoutSeconds" seconds
builder.Services.Configure<HostOptions>(o =>
    o.ShutdownTimeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Worker:ShutdownTimeoutSeconds", 30)));

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
