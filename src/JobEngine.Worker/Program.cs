using JobEngine.Application.interfaces;
using JobEngine.Infrastructure;
using JobEngine.SampleJobs.handlers;
using JobEngine.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<WorkerService>();

builder.Services.AddJobEnginePersistence(builder.Configuration.GetConnectionString("Postgres")!);
builder.Services.AddScoped<IJobHandler, SleepHandler>();
builder.Services.AddScoped<IJobHandler, AlwaysFailHandler>();
builder.Services.AddScoped<IJobHandler, SendEmailHandler>();
builder.Services.AddScoped<IJobHandler, FailRandomlyHandler>();

var host = builder.Build();
host.Run();
