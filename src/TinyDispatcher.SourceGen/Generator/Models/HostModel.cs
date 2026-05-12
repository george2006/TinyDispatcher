using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record HostModel(
    DiscoveryResult Discovery,
    ReferencedAssemblyContributions ReferencedContributions,
    ImmutableArray<HostLane> Lanes);
