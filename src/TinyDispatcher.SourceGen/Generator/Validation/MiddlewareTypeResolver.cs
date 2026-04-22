#nullable enable

using Microsoft.CodeAnalysis;
using System;

namespace TinyDispatcher.SourceGen.Generator.Validation;

internal sealed class MiddlewareTypeResolver
{
    private readonly Compilation _compilation;

    public MiddlewareTypeResolver(Compilation compilation)
    {
        _compilation = compilation ?? throw new ArgumentNullException(nameof(compilation));
    }

    public INamedTypeSymbol? Resolve(string openTypeFqn, int arity)
    {
        if (string.IsNullOrWhiteSpace(openTypeFqn))
            return null;

        var metadataName = openTypeFqn.StartsWith("global::", StringComparison.Ordinal)
            ? openTypeFqn.Substring("global::".Length)
            : openTypeFqn;

        return arity > 0
            ? ResolveMetadataName(metadataName + "`" + arity.ToString())
            : ResolveMetadataName(metadataName);
    }

    private INamedTypeSymbol? ResolveMetadataName(string metadataName)
    {
        var resolved = _compilation.GetTypeByMetadataName(metadataName);
        if (resolved is not null)
            return resolved;

        var lastDotIndex = metadataName.LastIndexOf('.');
        if (lastDotIndex < 0)
            return null;

        var nestedMetadataName =
            metadataName.Substring(0, lastDotIndex) + "+" + metadataName.Substring(lastDotIndex + 1);

        return _compilation.GetTypeByMetadataName(nestedMetadataName);
    }
}
