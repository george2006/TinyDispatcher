#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Generator.Options;

namespace TinyDispatcher.SourceGen.Generator.Analysis;

internal static class GeneratorAnalysisPhase
{
    public static GeneratorAnalysisResult Analyze(
        Compilation compilation,
        ImmutableArray<InvocationExpressionSyntax> useTinyCallsSyntax,
        AnalyzerConfigOptionsProvider optionsProvider)
    {
        GuardInputs(compilation);

        var contextInference = new ContextInference();
        var bootstrapLambdaExtractor = new BootstrapLambdaExtractor();
        var semanticFilter = new UseTinyDispatcherSemanticFilter();
        var optionsFactory = new GeneratorOptionsFactory(new OptionsProvider());
        var confirmedUseTinyCallsSyntax = semanticFilter.Filter(compilation, useTinyCallsSyntax);
        var confirmedBootstrapLambdas =
            bootstrapLambdaExtractor.Extract(compilation, confirmedUseTinyCallsSyntax);
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

        return new GeneratorAnalysisResult(
            Analysis: new GeneratorAnalysis(
                EffectiveOptions: effectiveOptions,
                HostBootstrap: hostBootstrap),
            ConfirmedBootstrapLambdas: confirmedBootstrapLambdas);
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

        var hasSingleInferredContext = contextInference.TryInferSingleContextTypeFromResolvedCalls(
            useTinyDispatcherCalls,
            out var inferredContextFqn);

        if (!hasSingleInferredContext)
        {
            return baseOptions;
        }

        return optionsFactory.ApplyInferredContextIfMissing(baseOptions, inferredContextFqn);
    }

    private static HostBootstrapInfo BuildHostBootstrapInfo(
        ImmutableArray<InvocationExpressionSyntax> useTinyCallsSyntax,
        GeneratorOptions effectiveOptions,
        ImmutableArray<UseTinyDispatcherCall> useTinyDispatcherCalls)
    {
        return new HostBootstrapInfo(
            IsHostProject: useTinyCallsSyntax.Length > 0,
            ConfiguredContextFqn: GetConfiguredContextFqn(effectiveOptions),
            LaneDeclarations: BuildHostLaneDeclarations(useTinyDispatcherCalls));
    }

    private static string GetConfiguredContextFqn(GeneratorOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.CommandContextType))
        {
            return string.Empty;
        }

        return Fqn.EnsureGlobal(options.CommandContextType!);
    }

    private static ImmutableArray<HostLaneDeclaration> BuildHostLaneDeclarations(
        ImmutableArray<UseTinyDispatcherCall> useTinyDispatcherCalls)
    {
        if (useTinyDispatcherCalls.IsDefaultOrEmpty)
        {
            return ImmutableArray<HostLaneDeclaration>.Empty;
        }

        var contextOrder = new List<string>();
        var callsByContext = GroupCallsByContext(useTinyDispatcherCalls, contextOrder);
        return BuildHostLaneDeclarations(contextOrder, callsByContext);
    }

    private static Dictionary<string, ImmutableArray<UseTinyDispatcherCall>.Builder> GroupCallsByContext(
        ImmutableArray<UseTinyDispatcherCall> calls,
        List<string> contextOrder)
    {
        var groups = new Dictionary<string, ImmutableArray<UseTinyDispatcherCall>.Builder>(
            StringComparer.Ordinal);

        for (var i = 0; i < calls.Length; i++)
        {
            var call = calls[i];
            var builder = GetOrAddContextBuilder(groups, contextOrder, call.ContextTypeFqn);
            builder.Add(call);
        }

        return groups;
    }

    private static ImmutableArray<UseTinyDispatcherCall>.Builder GetOrAddContextBuilder(
        Dictionary<string, ImmutableArray<UseTinyDispatcherCall>.Builder> groups,
        List<string> contextOrder,
        string contextTypeFqn)
    {
        if (groups.TryGetValue(contextTypeFqn, out var builder))
        {
            return builder;
        }

        builder = ImmutableArray.CreateBuilder<UseTinyDispatcherCall>();
        groups.Add(contextTypeFqn, builder);
        contextOrder.Add(contextTypeFqn);

        return builder;
    }

    private static ImmutableArray<HostLaneDeclaration> BuildHostLaneDeclarations(
        List<string> contextOrder,
        Dictionary<string, ImmutableArray<UseTinyDispatcherCall>.Builder> callsByContext)
    {
        var declarations = ImmutableArray.CreateBuilder<HostLaneDeclaration>(callsByContext.Count);

        for (var i = 0; i < contextOrder.Count; i++)
        {
            var contextTypeFqn = contextOrder[i];
            declarations.Add(new HostLaneDeclaration(contextTypeFqn, callsByContext[contextTypeFqn].ToImmutable()));
        }

        return declarations.ToImmutable();
    }
}
