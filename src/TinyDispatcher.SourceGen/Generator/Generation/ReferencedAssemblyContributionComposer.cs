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
            MergePerCommandContributions(perCommand, assembly.PerCommandMiddlewareFindings);
            MergePolicyContributions(policies, assembly.PolicyFindings);
            globals.AddRange(assembly.Globals);
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
        ImmutableArray<PerCommandMiddlewareFinding> source)
    {
        for (var i = 0; i < source.Length; i++)
        {
            var finding = source[i];

            if (!target.ContainsKey(finding.CommandTypeFqn))
                target[finding.CommandTypeFqn] = finding.Middlewares;
        }
    }

    private static void MergePolicyContributions(
        ImmutableDictionary<string, PolicySpec>.Builder target,
        ImmutableArray<PolicyFinding> source)
    {
        for (var i = 0; i < source.Length; i++)
        {
            var finding = source[i];

            if (!target.ContainsKey(finding.PolicyTypeFqn))
                target[finding.PolicyTypeFqn] = ToPolicySpec(finding);
        }
    }

    private static PolicySpec ToPolicySpec(PolicyFinding finding)
    {
        return new PolicySpec(
            finding.PolicyTypeFqn,
            finding.Middlewares,
            finding.Commands);
    }
}
