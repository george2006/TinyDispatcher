#nullable enable

using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

/// <summary>
/// Represents an open generic middleware type discovered from bootstrap or policy configuration.
/// Invariant:
///   - OpenTypeSymbol is OriginalDefinition
///   - OpenTypeFqn is the base fully-qualified name WITHOUT generic arguments
///   - Arity matches OpenTypeSymbol.Arity
/// </summary>
public readonly record struct MiddlewareRef(
    INamedTypeSymbol OpenTypeSymbol,
    string OpenTypeFqn,
    int Arity)
{
    internal static ImmutableArray<MiddlewareRef> FromOrderedEntries(ImmutableArray<OrderedEntry> globals)
    {
        throw new NotImplementedException();
    }
}