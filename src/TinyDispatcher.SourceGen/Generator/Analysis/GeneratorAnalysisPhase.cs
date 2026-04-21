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
        var useTinyDispatcherCalls =
            contextInference.ResolveAllUseTinyDispatcherContexts(confirmedUseTinyCallsSyntax, compilation);

        var effectiveOptions = ResolveEffectiveOptions(
            compilation,
            optionsProvider,
            useTinyDispatcherCalls,
            contextInference,
            optionsFactory);

        var hostBootstrap = BuildHostBootstrapInfo(
            confirmedUseTinyCallsSyntax,
            effectiveOptions,
            useTinyDispatcherCalls);

        return new GeneratorAnalysis(
            Compilation: compilation,
            UseTinyCallsSyntax: confirmedUseTinyCallsSyntax,
            EffectiveOptions: effectiveOptions,
            HostBootstrap: hostBootstrap);
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
        ImmutableArray<UseTinyDispatcherCall> useTinyDispatcherCalls,
        ContextInference contextInference,
        GeneratorOptionsFactory optionsFactory)
    {
        var baseOptions = optionsFactory.Create(compilation, optionsProvider);

        var inferredContextFqn = contextInference.TryInferContextTypeFromResolvedCalls(useTinyDispatcherCalls);

        return optionsFactory.ApplyInferredContextIfMissing(baseOptions, inferredContextFqn);
    }

    private static HostBootstrapInfo BuildHostBootstrapInfo(
        ImmutableArray<InvocationExpressionSyntax> useTinyCallsSyntax,
        GeneratorOptions effectiveOptions,
        ImmutableArray<UseTinyDispatcherCall> useTinyDispatcherCalls)
    {
        return new HostBootstrapInfo(
            IsHostProject: useTinyCallsSyntax.Length > 0,
            ExpectedContextFqn: GetExpectedContextFqn(effectiveOptions),
            UseTinyDispatcherCalls: useTinyDispatcherCalls);
    }

    private static string GetExpectedContextFqn(GeneratorOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.CommandContextType))
        {
            return string.Empty;
        }

        return Fqn.EnsureGlobal(options.CommandContextType!);
    }
}
