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
        ImmutableArray<MiddlewareRef> globals,
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> perCommand,
        ImmutableDictionary<string, PolicySpec> policies,
        GeneratorOptions options)
    {
        _globals = PipelineMiddlewareSets.NormalizeDistinct(globals);
        _perCommand = NormalizePerCommand(perCommand);
        _policyByCommand = BuildPolicyIndex(policies);
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

    private static IReadOnlyDictionary<string, MiddlewareRef[]> NormalizePerCommand(
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> perCommand)
    {
        var dict = new Dictionary<string, MiddlewareRef[]>(StringComparer.Ordinal);

        foreach (var kv in perCommand)
        {
            var cmd = PipelineTypeNames.NormalizeFqn(kv.Key);
            if (string.IsNullOrWhiteSpace(cmd))
                continue;

            var mids = PipelineMiddlewareSets.NormalizeDistinct(kv.Value);
            if (mids.Length == 0)
                continue;

            dict[cmd] = mids;
        }

        return dict;
    }

    private sealed record PolicyContribution(string PolicyTypeFqn, MiddlewareRef[] Middlewares);

    // First policy wins (ordered by policy type name, deterministic)
    private static IReadOnlyDictionary<string, PolicyContribution> BuildPolicyIndex(
        ImmutableDictionary<string, PolicySpec> policies)
    {
        var map = new Dictionary<string, PolicyContribution>(StringComparer.Ordinal);

        var orderedPolicies = PipelinePolicyOrdering.GetPoliciesInStableOrder(policies);
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

            AddPolicyCommands(map, p, policyType, mids);
        }

        return map;
    }

    private static void AddPolicyCommands(
        Dictionary<string, PolicyContribution> map,
        PolicySpec policy,
        string policyType,
        MiddlewareRef[] middlewares)
    {
        var contribution = new PolicyContribution(policyType, middlewares);

        for (var commandIndex = 0; commandIndex < policy.Commands.Length; commandIndex++)
        {
            var command = PipelineTypeNames.NormalizeFqn(policy.Commands[commandIndex]);
            var commandIsMissing = string.IsNullOrWhiteSpace(command);

            if (commandIsMissing)
            {
                continue;
            }

            var commandAlreadyHasPolicy = map.ContainsKey(command);
            if (commandAlreadyHasPolicy)
            {
                continue;
            }

            map[command] = contribution;
        }
    }

}
