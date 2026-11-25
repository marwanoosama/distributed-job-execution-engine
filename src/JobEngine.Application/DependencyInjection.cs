using JobEngine.Application.services.retry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobEngine.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddRetryPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(RetryPolicyOptions.SectionName).Get<RetryPolicyOptions>() ?? new RetryPolicyOptions();
        RetryPolicy.Configure(options);
        return services;
    }
}
