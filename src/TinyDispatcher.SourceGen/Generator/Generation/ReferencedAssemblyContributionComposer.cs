#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Generation;

internal static class ReferencedAssemblyContributionComposer
{
    public static DiscoveryResult MergeDiscovery(
        DiscoveryResult discovery,
        ReferencedAssemblyContributions referencedContributions,
        string expectedContextFqn)
    {
        if (!referencedContributions.HasCommands())
            return discovery;

        var commands = ImmutableArray.CreateBuilder<HandlerContract>(discovery.Commands.Length);
        var seen = new HashSet<string>(System.StringComparer.Ordinal);

        AddCommands(commands, seen, discovery.Commands);
        AddCommands(commands, seen, referencedContributions.EnumerateCommands(expectedContextFqn));

        return new DiscoveryResult(
            commands.ToImmutable(),
            discovery.Queries);
    }

    public static PipelineConfig MergePipelineConfig(
        PipelineConfig pipeline,
        ReferencedAssemblyContributions referencedContributions,
        string expectedContextFqn)
    {
        var perCommand = ImmutableDictionary.CreateBuilder<string, ImmutableArray<MiddlewareRef>>(System.StringComparer.Ordinal);
        foreach (var pair in pipeline.PerCommand)
            perCommand[pair.Key] = pair.Value;

        var policies = ImmutableDictionary.CreateBuilder<string, PolicySpec>(System.StringComparer.Ordinal);
        foreach (var pair in pipeline.Policies)
            policies[pair.Key] = pair.Value;

        foreach (var assembly in referencedContributions.EnumerateMatchingContext(expectedContextFqn))
        {
            MergePerCommandContributions(perCommand, assembly.PerCommand);
            MergePolicyContributions(policies, assembly.Policies);
        }

        return new PipelineConfig(
            pipeline.Globals,
            perCommand.ToImmutable(),
            policies.ToImmutable());
    }

    private static void AddCommands(
        ImmutableArray<HandlerContract>.Builder target,
        HashSet<string> seen,
        IEnumerable<HandlerContract> source)
    {
        foreach (var command in source)
        {
            var key = command.MessageTypeFqn + "|" + command.HandlerTypeFqn + "|" + command.ContextTypeFqn;
            if (seen.Add(key))
                target.Add(command);
        }
    }

    private static void MergePerCommandContributions(
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Builder target,
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> source)
    {
        foreach (var pair in source)
        {
            if (!target.ContainsKey(pair.Key))
                target[pair.Key] = pair.Value;
        }
    }

    private static void MergePolicyContributions(
        ImmutableDictionary<string, PolicySpec>.Builder target,
        ImmutableDictionary<string, PolicySpec> source)
    {
        foreach (var pair in source)
        {
            if (!target.ContainsKey(pair.Key))
                target[pair.Key] = pair.Value;
        }
    }
}
