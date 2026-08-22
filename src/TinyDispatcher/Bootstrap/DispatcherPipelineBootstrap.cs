#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher.Bootstrap;

/// <summary>
/// Stores DI registrations for generated command pipelines contributed by consumer assemblies.
/// Applied once during startup by the core DI entry point.
/// </summary>
public static class DispatcherPipelineBootstrap
{
    public static void AddContribution(AssemblyContribution contribution)
        => PipelineContributionStore.Add(contribution);

    public static void Apply(IServiceCollection services)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        // Apply only once per IServiceCollection to avoid duplicate DI registrations.
        if (services.Any(d => d.ServiceType == typeof(DispatcherPipelineBootstrapAppliedMarker)))
            return;

        services.AddSingleton<DispatcherPipelineBootstrapAppliedMarker>();

        var contributions = PipelineContributionStore.GetSnapshot();
        foreach (var c in contributions)
            c.Apply(services);
    }

    public static IReadOnlyList<DispatcherOperation> GetOperations()
    {
        var contributions = PipelineContributionStore.GetSnapshot();
        var operations = new List<DispatcherOperation>();

        foreach (var contribution in contributions)
        {
            operations.AddRange(contribution.GetOperationSnapshot());
        }

        return operations.ToArray();
    }

    private sealed class DispatcherPipelineBootstrapAppliedMarker { }
}
