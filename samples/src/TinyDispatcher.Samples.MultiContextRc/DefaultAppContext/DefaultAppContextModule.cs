using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher;
using TinyDispatcher.Context;
using TinyDispatcher.Pipeline;
using TinyDispatcher.Samples.MultiContextRc.Shared;
using TinyAppContext = TinyDispatcher.AppContext;

namespace TinyDispatcher.Samples.MultiContextRc.DefaultAppContext;

public static class DefaultAppContextModule
{
    public static IServiceCollection AddDefaultAppContextLane(this IServiceCollection services)
    {
        services.AddTransient(typeof(ConsoleLogMiddleware<,>));
        services.AddScoped<IFeatureInitializer, SampleFeatureInitializer>();

        services.UseTinyDispatcher<TinyAppContext>(tiny =>
        {
            tiny.UseGlobalMiddleware(typeof(ConsoleLogMiddleware<,>));
        });

        return services;
    }
}

public sealed record ShowFeature(string Name) : ICommand;

public sealed class ShowFeatureHandler : ICommandHandler<ShowFeature, TinyAppContext>
{
    public Task HandleAsync(ShowFeature command, TinyAppContext context, CancellationToken ct = default)
    {
        var feature = context.GetFeature<SampleFeature>();
        Console.WriteLine($"handler app-context {command.Name} feature={feature.Value}");
        return Task.CompletedTask;
    }
}

public sealed class SampleFeatureInitializer : IFeatureInitializer
{
    private readonly SampleClock _clock;

    public SampleFeatureInitializer(SampleClock clock)
    {
        _clock = clock;
    }

    public void Initialize(IFeatureCollection features)
    {
        features.Add(new SampleFeature("initialized@" + _clock.UtcNow));
    }
}

public sealed record SampleFeature(string Value);
