#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Extraction;

internal sealed class GeneratorExtractionPhase
{
    private readonly HandlerDiscoveryExtractor _handlerDiscoveryExtractor = new();
    private readonly PipelineConfigExtractor _pipelineConfigExtractor = new();

    public GeneratorExtraction Extract(
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol> handlerSymbols,
        ImmutableArray<ConfirmedBootstrapLambda> confirmedBootstrapLambdas,
        GeneratorOptions options)
    {
        var discovery = _handlerDiscoveryExtractor.Extract(compilation, handlerSymbols, options);
        var pipeline = _pipelineConfigExtractor.Extract(confirmedBootstrapLambdas);

        return new GeneratorExtraction(
            discovery,
            pipeline);
    }
}
