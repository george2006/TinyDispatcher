using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record ReferencedPerCommandMiddlewareContribution(
    string CommandTypeFqn,
    ImmutableArray<MiddlewareRef> Middlewares,
    string? ContextTypeFqn = null);
