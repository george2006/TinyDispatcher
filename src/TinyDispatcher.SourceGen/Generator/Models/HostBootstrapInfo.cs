using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record HostBootstrapInfo(
    bool IsHostProject,
    string ConfiguredContextFqn,
    ImmutableArray<HostContextInfo> Contexts = default);
