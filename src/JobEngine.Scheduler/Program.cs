using JobEngine.Scheduler;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<SchedulerService>();

var host = builder.Build();
host.Run();
