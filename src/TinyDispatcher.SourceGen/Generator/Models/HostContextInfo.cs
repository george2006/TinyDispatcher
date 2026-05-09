using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record HostContextInfo(
    string ContextTypeFqn,
    ImmutableArray<UseTinyDispatcherCall> UseTinyDispatcherCalls);
