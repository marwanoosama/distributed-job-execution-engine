using JobEngine.Application.interfaces;
using JobEngine.Infrastructure.JobQueue;
using JobEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace JobEngine.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddJobEnginePersistence(this IServiceCollection services, string postgresConnectionString, string redisConnectionString)
    {
        services.AddDbContext<JobDbContext>(options => options.UseNpgsql(postgresConnectionString).UseSnakeCaseNamingConvention());
        
        services.AddScoped<IJobDbContext>(sp => sp.GetRequiredService<JobDbContext>());
        
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redisConnectionString);
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton<IJobQueue, RedisJobQueue>();
        services.AddSingleton<ILeaseManager, RedisLeaseManager>();
        return services;
    }
}
