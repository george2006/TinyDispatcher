#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Discovery;
using TinyDispatcher.SourceGen.Generator.Analysis;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Extraction;

internal sealed class GeneratorExtractionPhase
{
    private readonly TinyBootstrapInvocationExtractor _invocationExtractor = new();
    private readonly PolicySpecBuilder _policyBuilder = new();
    private readonly MiddlewareOrdering _ordering = new();

    public GeneratorExtraction Extract(
        GeneratorAnalysis analysis,
        ImmutableArray<INamedTypeSymbol> handlerSymbols)
    {
        return Extract(
            analysis.Compilation,
            handlerSymbols,
            analysis.UseTinyCallsSyntax,
            analysis.EffectiveOptions);
    }

    public GeneratorExtraction Extract(
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol> handlerSymbols,
        ImmutableArray<InvocationExpressionSyntax> useTinyCallsSyntax,
        GeneratorOptions options)
    {
        var discovery = DiscoverHandlers(compilation, handlerSymbols, options);
        var pipeline = ExtractPipelines(compilation, useTinyCallsSyntax);

        return new GeneratorExtraction(
            discovery,
            pipeline);
    }

    private static DiscoveryResult DiscoverHandlers(
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

    private PipelineConfig ExtractPipelines(
        Compilation compilation,
        ImmutableArray<InvocationExpressionSyntax> useTinyCallsSyntax)
    {
        var globalEntries = new List<OrderedEntry>();
        var perCommandEntries = new List<OrderedPerCommandEntry>();
        var policyTypeSymbols = new List<INamedTypeSymbol>();

        for (var i = 0; i < useTinyCallsSyntax.Length; i++)
        {
            _invocationExtractor.Extract(
                useTinyCallsSyntax[i],
                compilation,
                globalEntries,
                perCommandEntries,
                policyTypeSymbols);
        }

        return new PipelineConfig(
            _ordering.OrderAndDistinctGlobals(globalEntries),
            _ordering.BuildPerCommandMap(perCommandEntries),
            _policyBuilder.Build(policyTypeSymbols));
    }
}
