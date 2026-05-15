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

        foreach (var referencedAssembly in referencedContributions.EnumerateMatchingContext(contextFqn))
        {
            MergeAssemblyPipeline(
                referencedAssembly,
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
        ReferencedAssemblyContribution referencedAssembly,
        string contextFqn,
        ImmutableArray<MiddlewareRef>.Builder globals,
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Builder perCommand,
        ImmutableDictionary<string, PolicySpec>.Builder policies)
    {
        globals.AddRange(referencedAssembly.Globals);

        MergePerCommandContributions(
            perCommand,
            referencedAssembly,
            contextFqn);
        MergePolicyContributions(
            policies,
            referencedAssembly,
            contextFqn);
    }

    private static void MergePerCommandContributions(
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Builder target,
        ReferencedAssemblyContribution referencedAssembly,
        string contextFqn)
    {
        for (var i = 0; i < referencedAssembly.PerCommandMiddlewareContributions.Length; i++)
        {
            var contribution = referencedAssembly.PerCommandMiddlewareContributions[i];

            var contributionBelongsToAnotherContext = !ContextMatching.Matches(
                contribution.ContextTypeFqn,
                contextFqn);
            if (contributionBelongsToAnotherContext)
            {
                continue;
            }

            var commandWasAlreadyConfigured = target.ContainsKey(contribution.CommandTypeFqn);
            if (!commandWasAlreadyConfigured)
            {
                target[contribution.CommandTypeFqn] = contribution.Middlewares;
            }
        }
    }

    private static void MergePolicyContributions(
        ImmutableDictionary<string, PolicySpec>.Builder target,
        ReferencedAssemblyContribution referencedAssembly,
        string contextFqn)
    {
        for (var i = 0; i < referencedAssembly.PolicyContributions.Length; i++)
        {
            var contribution = referencedAssembly.PolicyContributions[i];

            var contributionBelongsToAnotherContext = !ContextMatching.Matches(
                contribution.ContextTypeFqn,
                contextFqn);
            if (contributionBelongsToAnotherContext)
            {
                continue;
            }

            var policyHasNoCommandsForContext = contribution.Commands.Length == 0;
            if (policyHasNoCommandsForContext)
            {
                continue;
            }

            var policyWasAlreadyConfigured = target.ContainsKey(contribution.PolicyTypeFqn);
            if (!policyWasAlreadyConfigured)
            {
                target[contribution.PolicyTypeFqn] = ToPolicySpec(contribution);
            }
        }
    }

    private static PolicySpec ToPolicySpec(ReferencedPolicyContribution contribution)
    {
        return new PolicySpec(
            contribution.PolicyTypeFqn,
            contribution.Middlewares,
            contribution.Commands);
    }
}
