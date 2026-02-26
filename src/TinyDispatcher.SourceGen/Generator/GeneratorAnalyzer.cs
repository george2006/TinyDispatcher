#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Discovery;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Internal;
using TinyDispatcher.SourceGen.Validation;
using TinyDispatcher.SourceGen.Diagnostics;

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

        // Components (facts-only)
        var invocationExtractor = new TinyBootstrapInvocationExtractor();
        var policyBuilder = new PolicySpecBuilder();
        var ordering = new MiddlewareOrdering();
        var ctxInference = new ContextInference();
        var optionsFactory = new GeneratorOptionsFactory(new OptionsProvider());

        var effectiveOptions = ResolveEffectiveOptions(
            compilation,
            optionsProvider,
            useTinyCallsSyntax,
            ctxInference,
            optionsFactory);

        var expectedContextFqn = GetExpectedContextFqn(effectiveOptions);

        var discoveryResult = DiscoverHandlers(
            compilation,
            handlerSymbols,
            effectiveOptions);

        var pipelineFacts = ExtractPipelineFacts(
            compilation,
            useTinyCallsSyntax,
            invocationExtractor,
            ctxInference,
            policyBuilder,
            ordering);

        var validationContext = BuildValidationContext(
            compilation,
            discoveryResult,
            diagnosticsCatalog,
            useTinyCallsSyntax,
            expectedContextFqn,
            pipelineFacts);

        return new GeneratorAnalysis(
            Compilation: compilation,
            UseTinyCallsSyntax: useTinyCallsSyntax,
            Discovery: discoveryResult,
            EffectiveOptions: effectiveOptions,
            ValidationContext: validationContext,
            Globals: pipelineFacts.Globals,
            PerCommand: pipelineFacts.PerCommand,
            Policies: pipelineFacts.Policies);
    }

    private static void GuardInputs(
        Compilation compilation,
        DiagnosticsCatalog diagnosticsCatalog)
    {
        if (compilation is null)
            throw new ArgumentNullException(nameof(compilation));

        if (diagnosticsCatalog is null)
            throw new ArgumentNullException(nameof(diagnosticsCatalog));
    }

    private static GeneratorOptions ResolveEffectiveOptions(
        Compilation compilation,
        AnalyzerConfigOptionsProvider optionsProvider,
        ImmutableArray<InvocationExpressionSyntax> useTinyCallsSyntax,
        ContextInference ctxInference,
        GeneratorOptionsFactory optionsFactory)
    {
        var baseOptions = optionsFactory.Create(compilation, optionsProvider);

        var inferredCtxFqn =
            ctxInference.TryInferContextTypeFromUseTinyCalls(useTinyCallsSyntax, compilation);

        return optionsFactory.ApplyInferredContextIfMissing(baseOptions, inferredCtxFqn);
    }

    private static string GetExpectedContextFqn(GeneratorOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.CommandContextType))
            return string.Empty;

        return Fqn.EnsureGlobal(options.CommandContextType!);
    }

    private static DiscoveryResult DiscoverHandlers(
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol> handlerSymbols,
        GeneratorOptions effectiveOptions)
    {
        var handlerDiscovery = new RoslynHandlerDiscovery(
            Known.CoreNamespace,
            includeNamespacePrefix: effectiveOptions.IncludeNamespacePrefix,
            commandContextTypeFqn: effectiveOptions.CommandContextType);

        return handlerDiscovery.Discover(compilation, handlerSymbols);
    }

    private static PipelineFacts ExtractPipelineFacts(
        Compilation compilation,
        ImmutableArray<InvocationExpressionSyntax> useTinyCallsSyntax,
        TinyBootstrapInvocationExtractor invocationExtractor,
        ContextInference ctxInference,
        PolicySpecBuilder policyBuilder,
        MiddlewareOrdering ordering)
    {
        var useTinyDispatcherInvocations =
            UseTinyDispatcherInvocationExtractor.FindAllUseTinyDispatcherCalls(compilation);

        var allUseTinyDispatcherCalls =
            ctxInference.ResolveAllUseTinyDispatcherContexts(useTinyDispatcherInvocations, compilation);

        // Temp collections for extractor
        var globalEntries = new List<OrderedEntry>();
        var perCmdEntries = new List<OrderedPerCommandEntry>();
        var policyTypeSymbols = new List<INamedTypeSymbol>();

        for (var i = 0; i < useTinyCallsSyntax.Length; i++)
        {
            invocationExtractor.Extract(
                useTinyCallsSyntax[i],
                compilation,
                globalEntries,
                perCmdEntries,
                policyTypeSymbols);
        }

        var globals = ordering.OrderAndDistinctGlobals(globalEntries);
        var perCommand = ordering.BuildPerCommandMap(perCmdEntries);
        var policies = policyBuilder.Build(compilation, policyTypeSymbols);

        return new PipelineFacts(
            globals,
            perCommand,
            policies,
            allUseTinyDispatcherCalls);
    }

    private static GeneratorValidationContext BuildValidationContext(
        Compilation compilation,
        DiscoveryResult discoveryResult,
        DiagnosticsCatalog diagnosticsCatalog,
        ImmutableArray<InvocationExpressionSyntax> useTinyCallsSyntax,
        string expectedContextFqn,
        PipelineFacts pipelineFacts)
    {
        return new GeneratorValidationContext.Builder(
                compilation,
                discoveryResult,
                diagnosticsCatalog)
            .WithHostGate(useTinyCallsSyntax, isHost: useTinyCallsSyntax.Length > 0)
            .WithUseTinyDispatcherCalls(pipelineFacts.AllUseTinyCalls)
            .WithExpectedContext(expectedContextFqn)
            .WithPipelineConfig(
                pipelineFacts.Globals,
                pipelineFacts.PerCommand,
                pipelineFacts.Policies)
            .Build();
    }

    private sealed record PipelineFacts(
        ImmutableArray<MiddlewareRef> Globals,
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> PerCommand,
        ImmutableDictionary<string, PolicySpec> Policies,
        ImmutableArray<UseTinyDispatcherCall> AllUseTinyCalls);
}