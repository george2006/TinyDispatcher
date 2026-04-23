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

    public static void AddContribution(Action<IServiceCollection> contribution)
        => PipelineContributionStore.Add(contribution);

    public static IReadOnlyList<AssemblyContribution> GetContributions()
        => PipelineContributionStore.GetSnapshot();

    public static void Apply(IServiceCollection services)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        // Apply only once per IServiceCollection to avoid duplicate DI registrations.
        if (services.Any(d => d.ServiceType == typeof(DispatcherPipelineBootstrapAppliedMarker)))
            return;

        var contributions = PipelineContributionStore.GetSnapshot();
        var contributedAssemblyContributions = (IReadOnlyList<AssemblyContribution>)contributions;
        var contributedHandlerBindings = CollectHandlerBindings(contributions);
        var contributedCommandHandlers = CollectCommandHandlers(contributedHandlerBindings);

        services.AddSingleton<DispatcherPipelineBootstrapAppliedMarker>();
        services.AddSingleton(contributedAssemblyContributions);
        services.AddSingleton((IReadOnlyList<HandlerBinding>)contributedHandlerBindings);
        services.AddSingleton(contributedCommandHandlers);

        foreach (var c in contributions)
            c.Apply(services);
    }

    private static IReadOnlyList<HandlerBinding> CollectHandlerBindings(
        IReadOnlyList<AssemblyContribution> contributions)
    {
        if (contributions.Count == 0)
            return Array.Empty<HandlerBinding>();

        var handlers = new List<HandlerBinding>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < contributions.Count; i++)
        {
            var contribution = contributions[i];
            var commandHandlers = contribution.Handlers;
            if (commandHandlers.Count == 0)
                continue;

            for (int j = 0; j < commandHandlers.Count; j++)
            {
                var commandHandler = commandHandlers[j];
                var key =
                    commandHandler.CommandType.AssemblyQualifiedName + "|" +
                    commandHandler.HandlerType.AssemblyQualifiedName + "|" +
                    commandHandler.ContextType.AssemblyQualifiedName;

                if (!seen.Add(key))
                    continue;

                handlers.Add(commandHandler);
            }
        }

        if (handlers.Count == 0)
            return Array.Empty<HandlerBinding>();

        return handlers;
    }

    private static IReadOnlyList<CommandHandlerDescriptor> CollectCommandHandlers(
        IReadOnlyList<HandlerBinding> handlerBindings)
    {
        if (handlerBindings.Count == 0)
            return Array.Empty<CommandHandlerDescriptor>();

        var handlers = new List<CommandHandlerDescriptor>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < handlerBindings.Count; i++)
        {
            var binding = handlerBindings[i];
            var descriptor = new CommandHandlerDescriptor(
                CommandTypeFqn: ToDisplayName(binding.CommandType),
                HandlerTypeFqn: ToDisplayName(binding.HandlerType),
                ContextTypeFqn: ToDisplayName(binding.ContextType));

            var key =
                descriptor.CommandTypeFqn + "|" +
                descriptor.HandlerTypeFqn + "|" +
                descriptor.ContextTypeFqn;

            if (!seen.Add(key))
                continue;

            handlers.Add(descriptor);
        }

        if (handlers.Count == 0)
            return Array.Empty<CommandHandlerDescriptor>();

        return handlers;
    }

    private sealed class DispatcherPipelineBootstrapAppliedMarker { }

    private static string ToDisplayName(Type type)
    {
        var name = type.FullName ?? type.Name;
        return "global::" + name.Replace('+', '.');
    }
}
