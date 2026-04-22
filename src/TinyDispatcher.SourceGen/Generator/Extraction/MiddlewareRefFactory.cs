#nullable enable

using Microsoft.CodeAnalysis;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Extraction;

internal static class MiddlewareRefFactory
{
    public static MiddlewareRef Create(INamedTypeSymbol middlewareType)
    {
        var open = middlewareType.OriginalDefinition;

        var fqnWithArgs = Fqn.FromType(open);

        var genericSuffixIndex = fqnWithArgs.IndexOf('<');
        var baseFqn = genericSuffixIndex >= 0
            ? fqnWithArgs.Substring(0, genericSuffixIndex)
            : fqnWithArgs;

        return new MiddlewareRef(baseFqn, open.Arity);
    }
}
