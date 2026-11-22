using JobEngine.Application.interfaces;
using JobEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobEngine.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddJobEnginePersistence(this IServiceCollection services, string postgresConnectionString)
    {
        services.AddDbContext<JobDbContext>(options => options.UseNpgsql(postgresConnectionString).UseSnakeCaseNamingConvention());
        
        services.AddScoped<IJobDbContext>(sp => sp.GetRequiredService<JobDbContext>());

        return services;
    }
}
