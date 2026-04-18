#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Emitters.Pipelines;

internal sealed record PipelineContributions(
    MiddlewareRef[] Globals,
    IReadOnlyDictionary<string, MiddlewareRef[]> PerCommand,
    PipelinePolicyContribution[] Policies)
{
    public static PipelineContributions Create(
        ImmutableArray<MiddlewareRef> globals,
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> perCommand,
        ImmutableDictionary<string, PolicySpec> policies)
    {
        return new PipelineContributions(
            Globals: PipelineMiddlewareSets.NormalizeDistinct(globals),
            PerCommand: PipelinePerCommandMiddlewareMap.Build(perCommand),
            Policies: BuildPolicies(policies));
    }

    private static PipelinePolicyContribution[] BuildPolicies(
        ImmutableDictionary<string, PolicySpec> policies)
    {
        var orderedPolicies = PipelineOrdering.GetPoliciesInStableOrder(policies);
        var normalizedPolicies = new List<PipelinePolicyContribution>(orderedPolicies.Length);

        for (var i = 0; i < orderedPolicies.Length; i++)
        {
            var policy = orderedPolicies[i];
            var policyType = PipelineTypeNames.NormalizeFqn(policy.PolicyTypeFqn);
            var policyTypeIsMissing = string.IsNullOrWhiteSpace(policyType);

            if (policyTypeIsMissing)
            {
                continue;
            }

            var middlewares = PipelineMiddlewareSets.NormalizeDistinct(policy.Middlewares);
            var policyHasNoMiddlewares = middlewares.Length == 0;

            if (policyHasNoMiddlewares)
            {
                continue;
            }

            normalizedPolicies.Add(new PipelinePolicyContribution(
                policyType,
                middlewares,
                NormalizeCommands(policy.Commands)));
        }

        return normalizedPolicies.ToArray();
    }

    private static ImmutableArray<string> NormalizeCommands(ImmutableArray<string> commands)
    {
        if (commands.IsDefaultOrEmpty)
        {
            return ImmutableArray<string>.Empty;
        }

        var normalized = ImmutableArray.CreateBuilder<string>(commands.Length);

        for (var i = 0; i < commands.Length; i++)
        {
            var command = PipelineTypeNames.NormalizeFqn(commands[i]);

            if (!string.IsNullOrWhiteSpace(command))
            {
                normalized.Add(command);
            }
        }

        return normalized.ToImmutable();
    }
}
