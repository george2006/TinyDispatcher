using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record PipelineConfig(
    ImmutableArray<MiddlewareRef> Globals,
    ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> PerCommand,
    ImmutableDictionary<string, PolicySpec> Policies);
