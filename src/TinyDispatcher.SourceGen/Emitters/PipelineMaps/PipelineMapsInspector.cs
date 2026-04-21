#nullable enable

using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;

using TinyDispatcher.SourceGen.Generator.Options;

namespace TinyDispatcher.SourceGen.Emitters.PipelineMaps;

internal sealed class PipelineMapInspector
{
    private readonly MiddlewareRef[] _globals;
    private readonly IReadOnlyDictionary<string, MiddlewareRef[]> _perCommand;
    private readonly IReadOnlyDictionary<string, PipelinePolicyContribution> _policyByCommand;
    private readonly string _contextFqn;

    public PipelineMapInspector(
        PipelineContributions contributions,
        GeneratorOptions options)
    {
        _globals = contributions.Globals;
        _perCommand = contributions.PerCommand;
        _policyByCommand = contributions.PolicyByCommand;
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
    private IReadOnlyList<MiddlewareDescriptor> Compose(string messageFqn, PipelinePolicyContribution? policy)
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

    private static void AddPolicy(List<MiddlewareDescriptor> list, PipelinePolicyContribution? policy)
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

    private PipelinePolicyContribution? FindPolicy(string commandFqn)
        => _policyByCommand.TryGetValue(commandFqn, out var p) ? p : null;

    private static string[] PolicyApplied(PipelinePolicyContribution? policy)
    {
        if (policy is null)
        {
            return Array.Empty<string>();
        }

        return new[] { policy.PolicyTypeFqn };
    }

}
