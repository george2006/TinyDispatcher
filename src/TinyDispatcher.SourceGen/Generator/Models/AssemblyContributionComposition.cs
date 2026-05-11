using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record AssemblyContributionComposition(
    DiscoveryResult Discovery,
    ImmutableArray<HostContextProjection> Contexts);
