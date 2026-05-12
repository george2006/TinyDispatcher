using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record HostBootstrapInfo(
    bool IsHostProject,
    string ConfiguredContextFqn,
    ImmutableArray<HostLaneDeclaration> Contexts = default);
