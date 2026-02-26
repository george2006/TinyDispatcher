using Microsoft.Extensions.DependencyInjection;

namespace Performance.Tiny;

public static class TinyBenchmarkRegistration
{
    public static void AddGenerated(IServiceCollection services)
        => TinyDispatcher.Generated.ThisAssemblyPipelineContribution.Add(services);
}
