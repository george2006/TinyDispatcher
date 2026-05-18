using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record HostLaneDeclaration(
    string ContextTypeFqn,
    ImmutableArray<UseTinyDispatcherCall> UseTinyDispatcherCalls);
