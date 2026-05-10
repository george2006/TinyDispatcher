using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record ReferencedPolicyContribution(
    string PolicyTypeFqn,
    ImmutableArray<MiddlewareRef> Middlewares,
    ImmutableArray<string> Commands,
    string? ContextTypeFqn = null);
