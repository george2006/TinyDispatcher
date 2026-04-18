#nullable enable

using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Emitters.PipelineMaps;

internal sealed class PipelineMapInspector
{
    private readonly MiddlewareRef[] _globals;
    private readonly IReadOnlyDictionary<string, MiddlewareRef[]> _perCommand;
    private readonly IReadOnlyDictionary<string, PolicyContribution> _policyByCommand;
    private readonly string _contextFqn;

    public PipelineMapInspector(
        PipelineContributions contributions,
        GeneratorOptions options)
    {
        _globals = contributions.Globals;
        _perCommand = contributions.PerCommand;
        _policyByCommand = BuildPolicyIndex(contributions.Policies);
        _contextFqn = PipelineTypeNames.NormalizeFqn(options.CommandContextType!);
    }

    public PipelineDescriptor InspectCommand(HandlerContract handler)
        => BuildCommand(handler);

    public PipelineDescriptor InspectQuery(QueryHandlerContract handler)
        => BuildQuery(handler);

    private PipelineDescriptor BuildCommand(HandlerContract handler)
    {
        var command = PipelineTypeNames.NormalizeFqn(handler.MessageTypeFqn);
        var handlerFqn = PipelineTypeNames.NormalizeFqn(handler.HandlerTypeFqn);

        var policy = FindPolicy(command);
        var middlewares = Compose(command, policy);

        return new PipelineDescriptor(
            CommandFullName: command,
            ContextFullName: _contextFqn,
            HandlerFullName: handlerFqn,
            Middlewares: middlewares,
            PoliciesApplied: PolicyApplied(policy));
    }

    private PipelineDescriptor BuildQuery(QueryHandlerContract handler)
    {
        var query = PipelineTypeNames.NormalizeFqn(handler.QueryTypeFqn);
        var handlerFqn = PipelineTypeNames.NormalizeFqn(handler.HandlerTypeFqn);

        // Queries: global + per-command only (no policies today)
        var middlewares = Compose(query, policy: null);

        return new PipelineDescriptor(
            CommandFullName: query,
            ContextFullName: _contextFqn,
            HandlerFullName: handlerFqn,
            Middlewares: middlewares,
            PoliciesApplied: Array.Empty<string>());
    }

    // ORDER: Global -> Policy -> PerCommand (same mental model as pipelines)
    private IReadOnlyList<MiddlewareDescriptor> Compose(string messageFqn, PolicyContribution? policy)
    {
        var list = new List<MiddlewareDescriptor>();

        Add(list, _globals, "global");
        AddPolicy(list, policy);
        AddPerCommand(list, messageFqn);

        return list;
    }

    private static void Add(List<MiddlewareDescriptor> list, MiddlewareRef[] middlewares, string source)
    {
        for (var i = 0; i < middlewares.Length; i++)
        {
            list.Add(new MiddlewareDescriptor(middlewares[i].OpenTypeFqn, source));
        }
    }

    private static void AddPolicy(List<MiddlewareDescriptor> list, PolicyContribution? policy)
    {
        if (policy is null)
        {
            return;
        }

        Add(list, policy.Middlewares, "policy:" + policy.PolicyTypeFqn);
    }

    private void AddPerCommand(List<MiddlewareDescriptor> list, string messageFqn)
    {
        var hasPerCommandMiddlewares = _perCommand.TryGetValue(messageFqn, out var mids);

        if (!hasPerCommandMiddlewares)
        {
            return;
        }

        Add(list, mids, "per-command");
    }

    private PolicyContribution? FindPolicy(string commandFqn)
        => _policyByCommand.TryGetValue(commandFqn, out var p) ? p : null;

    private static string[] PolicyApplied(PolicyContribution? policy)
    {
        if (policy is null)
        {
            return Array.Empty<string>();
        }

        return new[] { policy.PolicyTypeFqn };
    }

    private sealed record PolicyContribution(string PolicyTypeFqn, MiddlewareRef[] Middlewares);

    // First policy wins (ordered by policy type name, deterministic)
    private static IReadOnlyDictionary<string, PolicyContribution> BuildPolicyIndex(
        ImmutableDictionary<string, PolicySpec> policies)
    {
        var map = new Dictionary<string, PolicyContribution>(StringComparer.Ordinal);

        var orderedPolicies = PipelineOrdering.GetPoliciesInStableOrder(policies);
        for (var i = 0; i < orderedPolicies.Length; i++)
        {
            var p = orderedPolicies[i];
            var policyType = PipelineTypeNames.NormalizeFqn(p.PolicyTypeFqn);
            var policyTypeIsMissing = string.IsNullOrWhiteSpace(policyType);

            if (policyTypeIsMissing)
            {
                continue;
            }

            var mids = PipelineMiddlewareSets.NormalizeDistinct(p.Middlewares);
            var policyHasNoMiddlewares = mids.Length == 0;

            if (policyHasNoMiddlewares)
            {
                continue;
            }

            var contribution = new PolicyContribution(policyType, mids);
            PipelinePolicyCommandMap.AddFirstPolicyWins(map, p.Commands, contribution);
        }

        return map;
    }

}
