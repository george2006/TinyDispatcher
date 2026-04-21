#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Emitters.Pipelines;

internal static class PipelinePerCommandMiddlewareMap
{
    public static Dictionary<string, MiddlewareRef[]> Build(
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> perCommand)
    {
        var normalized = new Dictionary<string, MiddlewareRef[]>(StringComparer.Ordinal);

        foreach (var pair in perCommand)
        {
            AddNormalizedMiddlewares(normalized, pair.Key, pair.Value);
        }

        return normalized;
    }

    private static void AddNormalizedMiddlewares(
        Dictionary<string, MiddlewareRef[]> normalized,
        string commandType,
        ImmutableArray<MiddlewareRef> middlewares)
    {
        var command = PipelineTypeNames.NormalizeFqn(commandType);
        var commandIsMissing = string.IsNullOrWhiteSpace(command);

        if (commandIsMissing)
        {
            return;
        }

        var distinctMiddlewares = PipelineMiddlewareSets.NormalizeDistinct(middlewares);
        var hasNoMiddlewares = distinctMiddlewares.Length == 0;

        if (hasNoMiddlewares)
        {
            return;
        }

        normalized[command] = distinctMiddlewares;
    }
}
