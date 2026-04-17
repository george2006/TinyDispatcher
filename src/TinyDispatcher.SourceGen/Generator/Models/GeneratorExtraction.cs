using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record GeneratorExtraction(
    DiscoveryResult Discovery,
    ImmutableArray<MiddlewareRef> Globals,
    ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> PerCommand,
    ImmutableDictionary<string, PolicySpec> Policies,
    ImmutableArray<UseTinyDispatcherCall> UseTinyDispatcherCalls);
