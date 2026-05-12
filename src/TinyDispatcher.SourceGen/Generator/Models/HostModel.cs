using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record HostModel(
    DiscoveryResult Discovery,
    ImmutableArray<HostLane> Lanes);
