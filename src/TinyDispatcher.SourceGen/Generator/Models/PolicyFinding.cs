using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record PolicyFinding(
    string PolicyTypeFqn,
    ImmutableArray<MiddlewareRef> Middlewares,
    ImmutableArray<string> Commands);
