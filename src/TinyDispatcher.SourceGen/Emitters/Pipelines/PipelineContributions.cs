#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Emitters.Pipelines;

internal sealed record PipelineContributions(
    MiddlewareRef[] Globals,
    IReadOnlyDictionary<string, MiddlewareRef[]> PerCommand,
    ImmutableDictionary<string, PolicySpec> Policies)
{
    public static PipelineContributions Create(
        ImmutableArray<MiddlewareRef> globals,
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> perCommand,
        ImmutableDictionary<string, PolicySpec> policies)
    {
        return new PipelineContributions(
            Globals: PipelineMiddlewareSets.NormalizeDistinct(globals),
            PerCommand: PipelinePerCommandMiddlewareMap.Build(perCommand),
            Policies: policies);
    }
}
