// TinyDispatcher.SourceGen/Generator.cs
// -----------------------------------------------------------------------------
// TinyDispatcher incremental source generator (netstandard2.0 friendly)
//
// ALWAYS emits:
//  - DispatcherModuleInitializer
//  - ThisAssemblyContribution
//  - EmptyPipelineContribution
//
// Conditionally emits (HOST ARTIFACTS):
//  - TinyDispatcherPipeline.g.cs       (when middleware/policies are declared via TinyBootstrap)
//
// HOST GATE (NO HEURISTICS, NO MSBUILD FLAGS):
//  - Only emit host artifacts if we find at least one call to:
//      services.UseTinyDispatcher<TContext>(tiny => { ... })
//    Discovery is SYNTAX-based.
// -----------------------------------------------------------------------------

#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using TinyDispatcher.SourceGen.Abstractions;
using TinyDispatcher.SourceGen.Discovery;
using TinyDispatcher.SourceGen.Emitters;
using TinyDispatcher.SourceGen.Emitters.Handlers;
using TinyDispatcher.SourceGen.Emitters.ModuleInitializer;
using TinyDispatcher.SourceGen.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Internal;
using TinyDispatcher.SourceGen.Validation;

namespace TinyDispatcher.SourceGen.Generator;

// ============================================================================
// Generator
// ============================================================================

[Generator]
public sealed class Generator : IIncrementalGenerator
{
    // =====================================================================
    // Entry point
    // =====================================================================
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var syntax = new UseTinyDispatcherSyntax();

        // ---------------------------------------------------------------------
        // Handler candidates (anchor – always produces)
        // ---------------------------------------------------------------------
        var handlerCandidates =
            context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (n, _) => n is ClassDeclarationSyntax,
                    static (ctx, ct) =>
                    {
                        var node = (ClassDeclarationSyntax)ctx.Node;
                        var model = ctx.SemanticModel;
                        return (INamedTypeSymbol?)model.GetDeclaredSymbol(node, ct);
                    })
                .Collect();

        // ---------------------------------------------------------------------
        // Find UseTinyDispatcher<TContext>(...) calls (SYNTAX-based)
        // IMPORTANT: do not rely on GetSymbolInfo here.
        // ---------------------------------------------------------------------
        var useTinyCalls =
            context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (n, _) => n is InvocationExpressionSyntax,
                    (ctx, _) =>
                    {
                        var inv = (InvocationExpressionSyntax)ctx.Node;
                        return syntax.IsUseTinyDispatcherInvocation(inv) ? inv : null;
                    })
                .Collect();

        var pipeline =
            context.CompilationProvider
                .Combine(handlerCandidates)
                .Combine(useTinyCalls)
                .Combine(context.AnalyzerConfigOptionsProvider);

        context.RegisterSourceOutput(pipeline, Execute);
    }

    // =====================================================================
    // EXECUTE
    // =====================================================================
    private static void Execute(
        SourceProductionContext spc,
        (((Compilation Compilation,
           ImmutableArray<INamedTypeSymbol?> Handlers) Left,
           ImmutableArray<InvocationExpressionSyntax?> UseTinyCalls) Left,
           AnalyzerConfigOptionsProvider Options) data)
    {
        var compilation = data.Left.Left.Compilation;

        var handlerSymbols = NormalizeHandlerSymbols(data.Left.Left.Handlers);
        var useTinyCallsSyntax = NormalizeUseTinyCalls(data.Left.UseTinyCalls);

        var roslyn = new RoslynGeneratorContext(spc);
        var diagnosticsCatalog = new DiagnosticsCatalog();

        var analysis = Analyze(
            compilation,
            handlerSymbols,
            useTinyCallsSyntax,
            data.Options,
            diagnosticsCatalog);

        var bag = Validate(analysis.ValidationContext);

        if (ReportAndHasErrors(roslyn, bag))
            return;

        var emitOptions = BuildEmitOptions(analysis);

        Emit(roslyn, analysis, emitOptions);
    }

    private static ImmutableArray<INamedTypeSymbol> NormalizeHandlerSymbols(
        ImmutableArray<INamedTypeSymbol?> handlers)
    {
        return handlers
            .Where(static s => s is not null)
            .Select(static s => s!)
            .ToImmutableArray();
    }

    private static ImmutableArray<InvocationExpressionSyntax> NormalizeUseTinyCalls(
        ImmutableArray<InvocationExpressionSyntax?> useTinyCalls)
    {
        return useTinyCalls
            .Where(static x => x is not null)
            .Select(static x => x!)
            .ToImmutableArray();
    }

    private static bool ReportAndHasErrors(RoslynGeneratorContext ctx, DiagnosticBag bag)
    {
        if (bag.Count == 0)
            return false;

        var arr = bag.ToImmutable();
        for (var i = 0; i < arr.Length; i++)
            ctx.ReportDiagnostic(arr[i]);

        return bag.HasErrors;
    }

    // =====================================================================
    // ANALYZE (facts-only)
    // =====================================================================
    private static GeneratorAnalysis Analyze(
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

    // =====================================================================
    // VALIDATE
    // =====================================================================
    private static DiagnosticBag Validate(GeneratorValidationContext vctx)
    {
        var bag = new DiagnosticBag();

        new ContextConsistencyValidator().Validate(vctx, bag);
        new DuplicateHandlerValidator().Validate(vctx, bag);
        new MiddlewareRefShapeValidator().Validate(vctx, bag);
        new PipelineDiagnosticsValidator().Validate(vctx, bag);

        return bag;
    }

    // =====================================================================
    // EMIT
    // =====================================================================
    private static GeneratorOptions BuildEmitOptions(GeneratorAnalysis analysis)
    {
        var vctx = analysis.ValidationContext;

        if (string.IsNullOrWhiteSpace(vctx.ExpectedContextFqn))
            return analysis.EffectiveOptions;

        var o = analysis.EffectiveOptions;

        return new GeneratorOptions(
            GeneratedNamespace: o.GeneratedNamespace,
            EmitDiExtensions: o.EmitDiExtensions,
            EmitHandlerRegistrations: o.EmitHandlerRegistrations,
            IncludeNamespacePrefix: o.IncludeNamespacePrefix,
            CommandContextType: vctx.ExpectedContextFqn,
            EmitPipelineMap: o.EmitPipelineMap,
            PipelineMapFormat: o.PipelineMapFormat);
    }

    private static void Emit(
        RoslynGeneratorContext roslyn,
        GeneratorAnalysis analysis,
        GeneratorOptions emitOptions)
    {
        var vctx = analysis.ValidationContext;

        new ModuleInitializerEmitter().Emit(roslyn, analysis.Discovery, emitOptions);
        new EmptyPipelineContributionEmitter().Emit(roslyn, analysis.Discovery, emitOptions);
        new HandlerRegistrationsEmitter().Emit(roslyn, analysis.Discovery, emitOptions);

        if (!vctx.IsHostProject)
            return;

        if (string.IsNullOrWhiteSpace(vctx.ExpectedContextFqn))
            return;

        var hasAnyPipelineContributions =
            vctx.Globals.Length > 0 ||
            vctx.PerCommand.Count > 0 ||
            vctx.Policies.Count > 0;

        if (!hasAnyPipelineContributions)
            return;

        new PipelineEmitter(vctx.Globals, vctx.PerCommand, vctx.Policies)
            .Emit(roslyn, analysis.Discovery, emitOptions);
    }
}