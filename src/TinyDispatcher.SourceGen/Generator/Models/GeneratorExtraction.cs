using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record GeneratorExtraction(
    DiscoveryResult Discovery,
    ReferencedAssemblyContributions ReferencedContributions,
    ImmutableArray<ContextPipelineConfig> ContextPipelines = default);
