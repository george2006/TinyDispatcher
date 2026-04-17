#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Diagnostics;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Internal;
using TinyDispatcher.SourceGen.Validation;

namespace TinyDispatcher.SourceGen.Generator;

internal static class GeneratorAnalyzer
{
    public static GeneratorAnalysis Analyze(
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol> handlerSymbols,
        ImmutableArray<InvocationExpressionSyntax> useTinyCallsSyntax,
        AnalyzerConfigOptionsProvider optionsProvider,
        DiagnosticsCatalog diagnosticsCatalog)
    {
        GuardInputs(compilation, diagnosticsCatalog);

        var contextInference = new ContextInference();
        var optionsFactory = new GeneratorOptionsFactory(new OptionsProvider());
        var extractionPhase = new GeneratorExtractionPhase();

        var effectiveOptions = ResolveEffectiveOptions(
            compilation,
            optionsProvider,
            useTinyCallsSyntax,
            contextInference,
            optionsFactory);

        var expectedContextFqn = GetExpectedContextFqn(effectiveOptions);

        var extraction = extractionPhase.Extract(
            compilation,
            handlerSymbols,
            useTinyCallsSyntax,
            effectiveOptions);

        var validationContext = BuildValidationContext(
            compilation,
            diagnosticsCatalog,
            useTinyCallsSyntax,
            expectedContextFqn,
            extraction);

        return new GeneratorAnalysis(
            Compilation: compilation,
            UseTinyCallsSyntax: useTinyCallsSyntax,
            EffectiveOptions: effectiveOptions,
            Extraction: extraction,
            ValidationContext: validationContext);
    }

    private static void GuardInputs(
        Compilation compilation,
        DiagnosticsCatalog diagnosticsCatalog)
    {
        if (compilation is null)
        {
            throw new ArgumentNullException(nameof(compilation));
        }

        if (diagnosticsCatalog is null)
        {
            throw new ArgumentNullException(nameof(diagnosticsCatalog));
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

    private static string GetExpectedContextFqn(GeneratorOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.CommandContextType))
        {
            return string.Empty;
        }

        return Fqn.EnsureGlobal(options.CommandContextType!);
    }

    private static GeneratorValidationContext BuildValidationContext(
        Compilation compilation,
        DiagnosticsCatalog diagnosticsCatalog,
        ImmutableArray<InvocationExpressionSyntax> useTinyCallsSyntax,
        string expectedContextFqn,
        GeneratorExtraction extraction)
    {
        return new GeneratorValidationContext.Builder(
                compilation,
                extraction.Discovery,
                diagnosticsCatalog)
            .WithHostGate(useTinyCallsSyntax, isHost: useTinyCallsSyntax.Length > 0)
            .WithUseTinyDispatcherCalls(extraction.UseTinyDispatcherCalls)
            .WithExpectedContext(expectedContextFqn)
            .WithPipelineConfig(
                extraction.Globals,
                extraction.PerCommand,
                extraction.Policies)
            .Build();
    }
}
