using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record GeneratorExtraction(
    DiscoveryResult Discovery,
    PipelineConfig Pipeline,
    ReferencedAssemblyContributions ReferencedContributions);
