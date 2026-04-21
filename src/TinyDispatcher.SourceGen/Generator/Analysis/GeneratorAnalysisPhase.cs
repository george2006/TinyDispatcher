#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Internal;

namespace TinyDispatcher.SourceGen.Generator.Analysis;

internal static class GeneratorAnalysisPhase
{
    public static GeneratorAnalysis Analyze(
        Compilation compilation,
        ImmutableArray<InvocationExpressionSyntax> useTinyCallsSyntax,
        AnalyzerConfigOptionsProvider optionsProvider)
    {
        GuardInputs(compilation);

        var contextInference = new ContextInference();
        var semanticFilter = new UseTinyDispatcherSemanticFilter();
        var optionsFactory = new GeneratorOptionsFactory(new OptionsProvider());
        var confirmedUseTinyCallsSyntax = semanticFilter.Filter(compilation, useTinyCallsSyntax);

        var effectiveOptions = ResolveEffectiveOptions(
            compilation,
            optionsProvider,
            confirmedUseTinyCallsSyntax,
            contextInference,
            optionsFactory);

        return new GeneratorAnalysis(
            Compilation: compilation,
            UseTinyCallsSyntax: confirmedUseTinyCallsSyntax,
            EffectiveOptions: effectiveOptions);
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
