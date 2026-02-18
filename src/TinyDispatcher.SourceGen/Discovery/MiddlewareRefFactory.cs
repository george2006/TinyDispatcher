#nullable enable

using Microsoft.CodeAnalysis;
using System;
using TinyDispatcher.SourceGen.Generator;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Discovery;

internal sealed class MiddlewareRefFactory
{
    public bool TryCreate(
        INamedTypeSymbol openMiddlewareType,
        out MiddlewareRef middleware)
    {
        middleware = default;

        if (openMiddlewareType is null)
            return false;

        var open = openMiddlewareType.OriginalDefinition;

        // Facts-only: we do NOT reject shapes here (validator will).
        // But we still normalize consistently so emitters are stable.

        var fmtNoGenerics = SymbolDisplayFormat.FullyQualifiedFormat
            .WithGenericsOptions(SymbolDisplayGenericsOptions.None);

        var openFqn = Fqn.EnsureGlobal(open.ToDisplayString(fmtNoGenerics));
        var arity = open.Arity;

        middleware = new MiddlewareRef(
            OpenTypeSymbol: open,
            OpenTypeFqn: openFqn,
            Arity: arity);

        return true;
    }
}
