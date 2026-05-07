using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record PerCommandMiddlewareFinding(
    string CommandTypeFqn,
    ImmutableArray<MiddlewareRef> Middlewares);
