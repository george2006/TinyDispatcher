#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Internal;

namespace TinyDispatcher.SourceGen.Generator;

internal static class GeneratorAnalyzer
{
    public static GeneratorAnalysis Analyze(
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol> handlerSymbols,
        ImmutableArray<InvocationExpressionSyntax> useTinyCallsSyntax,
        AnalyzerConfigOptionsProvider optionsProvider)
    {
        GuardInputs(compilation);

        var contextInference = new ContextInference();
        var optionsFactory = new GeneratorOptionsFactory(new OptionsProvider());
        var extractionPhase = new GeneratorExtractionPhase();

        var effectiveOptions = ResolveEffectiveOptions(
            compilation,
            optionsProvider,
            useTinyCallsSyntax,
            contextInference,
            optionsFactory);

        var extraction = extractionPhase.Extract(
            compilation,
            handlerSymbols,
            useTinyCallsSyntax,
            effectiveOptions);

        return new GeneratorAnalysis(
            Compilation: compilation,
            UseTinyCallsSyntax: useTinyCallsSyntax,
            EffectiveOptions: effectiveOptions,
            Extraction: extraction);
    }

    private static void GuardInputs(Compilation compilation)
    {
        if (compilation is null)
        {
            throw new ArgumentNullException(nameof(compilation));
        }
    }

    private static GeneratorOptions ResolveEffectiveOptions(
        Compilation compilation,
        AnalyzerConfigOptionsProvider optionsProvider,
        ImmutableArray<InvocationExpressionSyntax> useTinyCallsSyntax,
        ContextInference contextInference,
        GeneratorOptionsFactory optionsFactory)
    {
        var baseOptions = optionsFactory.Create(compilation, optionsProvider);

        var inferredContextFqn =
            contextInference.TryInferContextTypeFromUseTinyCalls(useTinyCallsSyntax, compilation);

        return optionsFactory.ApplyInferredContextIfMissing(baseOptions, inferredContextFqn);
    }
}
