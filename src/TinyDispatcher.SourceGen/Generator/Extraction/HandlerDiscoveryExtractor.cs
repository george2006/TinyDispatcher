#nullable enable

using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Extraction;

internal sealed class HandlerDiscoveryExtractor
{
    public DiscoveryResult Extract(
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol> handlerSymbols,
        GeneratorOptions options)
    {
        var handlerDiscovery = new RoslynHandlerDiscovery(
            Known.CoreNamespace,
            includeNamespacePrefix: options.IncludeNamespacePrefix,
            commandContextTypeFqn: options.CommandContextType);

        return handlerDiscovery.Discover(compilation, handlerSymbols);
    }
}
