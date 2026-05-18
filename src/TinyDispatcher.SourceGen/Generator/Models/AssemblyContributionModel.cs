using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record AssemblyContributionModel(
    DiscoveryResult Discovery,
    ImmutableArray<HostLane> Lanes,
    bool IsHostProject);
