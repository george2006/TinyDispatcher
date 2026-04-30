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
        var hasReferencedCommands = referencedContributions.HasCommands();
        if (!hasReferencedCommands)
        {
            return discovery;
        }

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
        var globals = ImmutableArray.CreateBuilder<MiddlewareRef>();
        globals.AddRange(pipeline.Globals);

        var perCommand = ImmutableDictionary.CreateBuilder<string, ImmutableArray<MiddlewareRef>>(System.StringComparer.Ordinal);
        foreach (var pair in pipeline.PerCommand)
        {
            perCommand[pair.Key] = pair.Value;
        }

        var policies = ImmutableDictionary.CreateBuilder<string, PolicySpec>(System.StringComparer.Ordinal);
        foreach (var pair in pipeline.Policies)
        {
            policies[pair.Key] = pair.Value;
        }

        foreach (var assembly in referencedContributions.EnumerateMatchingContext(expectedContextFqn))
        {
            globals.AddRange(assembly.Globals);
            MergePerCommandContributions(perCommand, assembly.PerCommand);
            MergePolicyContributions(policies, assembly.Policies);
        }

        return new PipelineConfig(
            globals.ToImmutable(),
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
            var isNewCommand = seen.Add(key);
            if (isNewCommand)
            {
                target.Add(command);
            }
        }
    }

    private static void MergePerCommandContributions(
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Builder target,
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> source)
    {
        foreach (var pair in source)
        {
            var commandAlreadyHasMiddlewares = target.ContainsKey(pair.Key);
            if (!commandAlreadyHasMiddlewares)
            {
                target[pair.Key] = pair.Value;
            }
        }
    }

    private static void MergePolicyContributions(
        ImmutableDictionary<string, PolicySpec>.Builder target,
        ImmutableDictionary<string, PolicySpec> source)
    {
        foreach (var pair in source)
        {
            var policyAlreadyExists = target.ContainsKey(pair.Key);
            if (!policyAlreadyExists)
            {
                target[pair.Key] = pair.Value;
            }
        }
    }
}
