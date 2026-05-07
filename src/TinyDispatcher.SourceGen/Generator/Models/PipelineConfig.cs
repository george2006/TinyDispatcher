using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record PipelineConfig(
    ImmutableArray<MiddlewareRef> Globals,
    ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> PerCommand,
    ImmutableDictionary<string, PolicySpec> Policies)
{
    public static PipelineConfig Empty { get; } = new(
        ImmutableArray<MiddlewareRef>.Empty,
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
        ImmutableDictionary<string, PolicySpec>.Empty);
}
