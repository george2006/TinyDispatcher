#nullable enable

using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Emitters.Pipelines; // TypeNames + MiddlewareSets
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
        _globals = MiddlewareSets.NormalizeDistinct(globals);
        _perCommand = NormalizePerCommand(perCommand);
        _policyByCommand = BuildPolicyIndex(policies);
        _contextFqn = TypeNames.NormalizeFqn(options.CommandContextType!);
    }

    public PipelineDescriptor InspectCommand(HandlerContract handler)
        => BuildCommand(handler);

    public PipelineDescriptor InspectQuery(QueryHandlerContract handler)
        => BuildQuery(handler);

    private PipelineDescriptor BuildCommand(HandlerContract handler)
    {
        var command = TypeNames.NormalizeFqn(handler.MessageTypeFqn);
        var handlerFqn = TypeNames.NormalizeFqn(handler.HandlerTypeFqn);

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
        var query = TypeNames.NormalizeFqn(handler.QueryTypeFqn);
        var handlerFqn = TypeNames.NormalizeFqn(handler.HandlerTypeFqn);

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
            list.Add(new MiddlewareDescriptor(middlewares[i].OpenTypeFqn, source));
    }

    private static void AddPolicy(List<MiddlewareDescriptor> list, PolicyContribution? policy)
    {
        if (policy is null)
            return;

        Add(list, policy.Middlewares, "policy:" + policy.PolicyTypeFqn);
    }

    private void AddPerCommand(List<MiddlewareDescriptor> list, string messageFqn)
    {
        if (!_perCommand.TryGetValue(messageFqn, out var mids))
            return;

        Add(list, mids, "per-command");
    }

    private PolicyContribution? FindPolicy(string commandFqn)
        => _policyByCommand.TryGetValue(commandFqn, out var p) ? p : null;

    private static string[] PolicyApplied(PolicyContribution? policy)
        => policy is null ? Array.Empty<string>() : new[] { policy.PolicyTypeFqn };

    private static IReadOnlyDictionary<string, MiddlewareRef[]> NormalizePerCommand(
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> perCommand)
    {
        var dict = new Dictionary<string, MiddlewareRef[]>(StringComparer.Ordinal);

        foreach (var kv in perCommand)
        {
            var cmd = TypeNames.NormalizeFqn(kv.Key);
            if (string.IsNullOrWhiteSpace(cmd))
                continue;

            var mids = MiddlewareSets.NormalizeDistinct(kv.Value);
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

        foreach (var p in policies.Values.OrderBy(x => TypeNames.NormalizeFqn(x.PolicyTypeFqn), StringComparer.Ordinal))
        {
            var policyType = TypeNames.NormalizeFqn(p.PolicyTypeFqn);
            if (string.IsNullOrWhiteSpace(policyType))
                continue;

            var mids = MiddlewareSets.NormalizeDistinct(p.Middlewares);
            if (mids.Length == 0)
                continue;

            for (var i = 0; i < p.Commands.Length; i++)
            {
                var cmd = TypeNames.NormalizeFqn(p.Commands[i]);
                if (string.IsNullOrWhiteSpace(cmd))
                    continue;

                map.Add(cmd, new PolicyContribution(policyType, mids));
            }
        }

        return map;
    }
}