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
    public static void AddContribution(IDispatcherAssemblyContribution contribution)
        => PipelineContributionStore.Add(contribution);

    public static void AddContribution(Action<IServiceCollection> contribution)
        => PipelineContributionStore.Add(contribution);

    public static void Apply(IServiceCollection services)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        // Apply only once per IServiceCollection to avoid duplicate DI registrations.
        if (services.Any(d => d.ServiceType == typeof(DispatcherPipelineBootstrapAppliedMarker)))
            return;

        var contributions = PipelineContributionStore.Drain();
        var contributedCommandHandlers = CollectCommandHandlers(contributions);

        services.AddSingleton<DispatcherPipelineBootstrapAppliedMarker>();
        services.AddSingleton(contributedCommandHandlers);

        foreach (var c in contributions)
            c.Apply(services);
    }

    private static IReadOnlyList<CommandHandlerDescriptor> CollectCommandHandlers(
        IReadOnlyList<IDispatcherAssemblyContribution> contributions)
    {
        if (contributions.Count == 0)
            return Array.Empty<CommandHandlerDescriptor>();

        var handlers = new List<CommandHandlerDescriptor>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < contributions.Count; i++)
        {
            var contribution = contributions[i];
            var commandHandlers = contribution.CommandHandlers;
            if (commandHandlers.Count == 0)
                continue;

            for (int j = 0; j < commandHandlers.Count; j++)
            {
                var commandHandler = commandHandlers[j];
                var key =
                    commandHandler.CommandTypeFqn + "|" +
                    commandHandler.HandlerTypeFqn + "|" +
                    commandHandler.ContextTypeFqn;

                if (!seen.Add(key))
                    continue;

                handlers.Add(commandHandler);
            }
        }

        if (handlers.Count == 0)
            return Array.Empty<CommandHandlerDescriptor>();

        return handlers;
    }

    private sealed class DispatcherPipelineBootstrapAppliedMarker { }

}
