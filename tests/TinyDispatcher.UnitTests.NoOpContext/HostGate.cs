using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher.UnitTests.NoOp;

using TinyDispatcher;

namespace TinyDispatcher.UnitTests.NoOp;
public static class NoOpTestBootstrap
{
    public static void Configure(IServiceCollection services)
    {
        services.AddSingleton<NoOpCallTracker>();

        services.UseTinyNoOpContext(options =>
        {
            options.UseGlobalMiddleware(typeof(NoOpGlobalMiddleware<,>));
            options.UseMiddlewareFor<NoOpTestCommand>(typeof(NoOpPerCommandMiddleware<,>));
        });

        TinyDispatcher.Generated.ThisAssemblyPipelineContribution.Add(services);
    }
}