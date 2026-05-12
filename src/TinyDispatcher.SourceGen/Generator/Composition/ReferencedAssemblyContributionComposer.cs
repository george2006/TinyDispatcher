#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Composition;

internal static class ReferencedAssemblyContributionComposer
{
    public static DiscoveryResult MergeDiscovery(
        DiscoveryResult discovery,
        ReferencedAssemblyContributions referencedContributions,
        string contextFqn)
    {
        var hasReferencedCommands = referencedContributions.HasCommands();
        if (!hasReferencedCommands)
        {
            return discovery;
        }

        var commands = ImmutableArray.CreateBuilder<HandlerContract>(discovery.Commands.Length);
        var seen = new HashSet<string>(System.StringComparer.Ordinal);

        AddCommands(commands, seen, discovery.Commands);
        AddCommands(commands, seen, referencedContributions.EnumerateCommands(contextFqn));

        return new DiscoveryResult(
            commands.ToImmutable(),
            discovery.Queries);
    }

    public static PipelineConfig MergePipelineConfig(
        PipelineConfig pipeline,
        ReferencedAssemblyContributions referencedContributions,
        string contextFqn)
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

        foreach (var assembly in referencedContributions.EnumerateMatchingContext(contextFqn))
        {
            MergeAssemblyPipeline(
                assembly,
                contextFqn,
                globals,
                perCommand,
                policies);
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

    private static void MergeAssemblyPipeline(
        ReferencedAssemblyContribution assembly,
        string contextFqn,
        ImmutableArray<MiddlewareRef>.Builder globals,
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Builder perCommand,
        ImmutableDictionary<string, PolicySpec>.Builder policies)
    {
        globals.AddRange(assembly.Globals);

        MergePerCommandContributions(
            perCommand,
            assembly,
            contextFqn);
        MergePolicyContributions(
            policies,
            assembly,
            contextFqn);
    }

    private static void MergePerCommandContributions(
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Builder target,
        ReferencedAssemblyContribution assembly,
        string contextFqn)
    {
        for (var i = 0; i < assembly.PerCommandMiddlewareContributions.Length; i++)
        {
            var finding = assembly.PerCommandMiddlewareContributions[i];

            var contributionBelongsToAnotherContext = !ContextMatching.Matches(
                finding.ContextTypeFqn,
                contextFqn);
            if (contributionBelongsToAnotherContext)
            {
                continue;
            }

            var commandWasAlreadyConfigured = target.ContainsKey(finding.CommandTypeFqn);
            if (!commandWasAlreadyConfigured)
            {
                target[finding.CommandTypeFqn] = finding.Middlewares;
            }
        }
    }

    private static void MergePolicyContributions(
        ImmutableDictionary<string, PolicySpec>.Builder target,
        ReferencedAssemblyContribution assembly,
        string contextFqn)
    {
        for (var i = 0; i < assembly.PolicyContributions.Length; i++)
        {
            var finding = assembly.PolicyContributions[i];

            var contributionBelongsToAnotherContext = !ContextMatching.Matches(
                finding.ContextTypeFqn,
                contextFqn);
            if (contributionBelongsToAnotherContext)
            {
                continue;
            }

            var policyHasNoCommandsForContext = finding.Commands.Length == 0;
            if (policyHasNoCommandsForContext)
            {
                continue;
            }

            var policyWasAlreadyConfigured = target.ContainsKey(finding.PolicyTypeFqn);
            if (!policyWasAlreadyConfigured)
            {
                target[finding.PolicyTypeFqn] = ToPolicySpec(finding);
            }
        }
    }

    private static PolicySpec ToPolicySpec(ReferencedPolicyContribution finding)
    {
        return new PolicySpec(
            finding.PolicyTypeFqn,
            finding.Middlewares,
            finding.Commands);
    }
}
