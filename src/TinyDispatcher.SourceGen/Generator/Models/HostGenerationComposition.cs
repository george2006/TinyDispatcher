using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record HostGenerationComposition(
    DiscoveryResult Discovery,
    ReferencedAssemblyContributions ReferencedContributions,
    ImmutableArray<ContextGenerationInput> Contexts);
