using JobEngine.Application.services.retry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobEngine.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddRetryPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RetryPolicyOptions>(configuration.GetSection(RetryPolicyOptions.SectionName));
        services.AddSingleton<RetryPolicy>();
        return services;
    }
}
