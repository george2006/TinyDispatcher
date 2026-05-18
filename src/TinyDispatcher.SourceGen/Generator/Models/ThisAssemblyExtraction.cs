using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record ThisAssemblyExtraction(
    DiscoveryResult Discovery,
    ImmutableArray<ContextPipeline> ContextPipelines = default);
