using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher.Dispatching;

namespace TinyDispatcher.Samples.Pipelines;

public static class TinyBootstrap
{
    public static IServiceCollection AddTiny(this IServiceCollection services)
    {
        services.UseTinyDispatcher<AppContext>(tiny =>
        {
            // Global middleware available in this project
            tiny.UseGlobalMiddleware(typeof(GlobalMiddlewareSample.GlobalLoggingMiddleware<,>));

            // Per-command middleware mapping
            tiny.UseMiddlewareFor<PerCommandMiddlewareSample.Pay>(typeof(PerCommandMiddlewareSample.OnlyForPayMiddleware<,>));

            // Policy (commands + middleware)
            tiny.UsePolicy<PolicySample.CheckoutPolicy>();
        });

        // Register middleware types used anywhere
        services.AddTransient(typeof(GlobalMiddlewareSample.GlobalLoggingMiddleware<,>));
        services.AddTransient(typeof(PerCommandMiddlewareSample.OnlyForPayMiddleware<,>));
        services.AddTransient(typeof(PolicySample.PolicyLoggingMiddleware<,>));
        services.AddTransient(typeof(PolicySample.PolicyValidationMiddleware<,>));

        return services;
    }
}
