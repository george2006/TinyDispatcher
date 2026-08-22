#nullable enable

using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;

using TinyDispatcher.SourceGen.Generator.Options;

namespace TinyDispatcher.SourceGen.Generator.Generation.Emitters.PipelineMaps;

internal sealed class PipelineMapInspector
{
    private readonly MiddlewareRef[] _globals;
    private readonly IReadOnlyDictionary<string, MiddlewareRef[]> _perCommand;
    private readonly string _contextFqn;

    public PipelineMapInspector(
        PipelineContributions contributions,
        GeneratorOptions options)
    {
        _globals = contributions.Globals;
        _perCommand = contributions.PerCommand;
        _contextFqn = PipelineTypeNames.NormalizeFqn(options.CommandContextType!);
    }

    public PipelineDescriptor InspectQuery(QueryHandlerContract handler)
        => BuildQuery(handler);

    private PipelineDescriptor BuildQuery(QueryHandlerContract handler)
    {
        var query = PipelineTypeNames.NormalizeFqn(handler.QueryTypeFqn);
        var handlerFqn = PipelineTypeNames.NormalizeFqn(handler.HandlerTypeFqn);

        var middlewares = Compose(query);

        return new PipelineDescriptor(
            CommandFullName: query,
            ContextFullName: _contextFqn,
            HandlerFullName: handlerFqn,
            Middlewares: middlewares,
            PoliciesApplied: Array.Empty<string>());
    }

    private IReadOnlyList<MiddlewareDescriptor> Compose(string messageFqn)
    {
        var list = new List<MiddlewareDescriptor>();

        Add(list, _globals, "global");
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

    private void AddPerCommand(List<MiddlewareDescriptor> list, string messageFqn)
    {
        var hasPerCommandMiddlewares = _perCommand.TryGetValue(messageFqn, out var mids);

        if (!hasPerCommandMiddlewares)
        {
            return;
        }

        Add(list, mids, "per-command");
    }

}

