#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher;

internal static class PipelineContributionStore
{
    private static readonly object _gate = new();
    private static readonly List<Action<IServiceCollection>> _items = new();

    public static void Add(Action<IServiceCollection> contribution)
    {
        if (contribution is null) return;
        lock (_gate)
        {
            _items.Add(contribution);
        }
    }

    public static Action<IServiceCollection>[] Drain()
    {
        lock (_gate)
        {
            // Snapshot: do NOT clear. Allows multiple ServiceProviders in the same process.
            return _items.ToArray();
        }
    }
}

/// <summary>
/// Stores DI registrations for generated command pipelines contributed by consumer assemblies.
/// Applied once during startup by the core DI entry point.
/// </summary>
public static class DispatcherPipelineBootstrap
{
    public static void AddContribution(Action<IServiceCollection> contribution)
        => PipelineContributionStore.Add(contribution);

    public static void Apply(IServiceCollection services)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        // Apply only once per IServiceCollection to avoid duplicate DI registrations.
        if (services.Any(d => d.ServiceType == typeof(DispatcherPipelineBootstrapAppliedMarker)))
            return;

        services.AddSingleton<DispatcherPipelineBootstrapAppliedMarker>();

        foreach (var c in PipelineContributionStore.Drain())
            c(services);
    }

    private sealed class DispatcherPipelineBootstrapAppliedMarker { }
}
