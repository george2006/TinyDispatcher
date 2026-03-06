#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher.Bootstrap;

namespace TinyDispatcher.Bootstrap;

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
