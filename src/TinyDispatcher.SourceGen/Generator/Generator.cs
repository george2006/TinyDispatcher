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

        // ---------------------------------------------------------------------
        // Normalize inputs
        // ---------------------------------------------------------------------
        var handlerSymbols = data.Left.Left.Handlers
            .Where(static s => s is not null)
            .Select(static s => s!)
            .ToImmutableArray();

        var useTinyCallsSyntax = data.Left.UseTinyCalls
            .Where(static x => x is not null)
            .Select(static x => x!)
            .ToImmutableArray();

        var roslyn = new RoslynGeneratorContext(spc);
        var diagnosticsCatalog = new DiagnosticsCatalog();

        // =====================================================================
        // ANALYZE (facts-only)
        // =====================================================================
        var analysis = Analyze(
            compilation,
            handlerSymbols,
            useTinyCallsSyntax,
            data.Options,
            diagnosticsCatalog);

        // =====================================================================
        // VALIDATE (all diagnostics)
        // =====================================================================
        var bag = Validate(analysis.ValidationContext);

        if (ReportAndHasErrors(roslyn, bag))
            return;

        // =====================================================================
        // EMIT (only after validation passes)
        // =====================================================================
        var emitOptions = BuildEmitOptions(analysis);

        Emit(roslyn, analysis, emitOptions);
    }

    static bool ReportAndHasErrors(RoslynGeneratorContext ctx, DiagnosticBag bag)
    {
        if (bag.Count == 0) 
            return false;

        var arr = bag.ToImmutable();
        for (var i = 0; i < arr.Length; i++)
            ctx.ReportDiagnostic(arr[i]);

        return bag.HasErrors;
    }

    private static GeneratorAnalysis Analyze(
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol> handlerSymbols,
        ImmutableArray<InvocationExpressionSyntax> useTinyCallsSyntax,
        AnalyzerConfigOptionsProvider optionsProvider,
        DiagnosticsCatalog diagnosticsCatalog)
    {
        if (compilation is null) throw new ArgumentNullException(nameof(compilation));
        if (diagnosticsCatalog is null) throw new ArgumentNullException(nameof(diagnosticsCatalog));

        // Components (facts-only)
        var invocationExtractor = new TinyBootstrapInvocationExtractor(); // facts-only
        var policyBuilder = new PolicySpecBuilder();                     // facts-only
        var ordering = new MiddlewareOrdering();
        var ctxInference = new ContextInference();
        var optionsFactory = new GeneratorOptionsFactory(new OptionsProvider());

        // ---------------------------------------------------------------------
        // 1) Read base options
        // ---------------------------------------------------------------------
        var baseOptions = optionsFactory.Create(compilation, optionsProvider);

        // ---------------------------------------------------------------------
        // 2) Infer context from SYNTAX-based UseTinyDispatcher calls
        //    (ContextInference must never infer "TContext" – it must ignore type params)
        // ---------------------------------------------------------------------
        var inferredCtxFqn = ctxInference.TryInferContextTypeFromUseTinyCalls(useTinyCallsSyntax, compilation);
        var effectiveOptions = optionsFactory.ApplyInferredContextIfMissing(baseOptions, inferredCtxFqn);

        var expectedContextFqn =
            string.IsNullOrWhiteSpace(effectiveOptions.CommandContextType)
                ? string.Empty
                : Fqn.EnsureGlobal(effectiveOptions.CommandContextType!);

        // ---------------------------------------------------------------------
        // 3) Discover handlers using the effective options (context-aware)
        // ---------------------------------------------------------------------
        var handlerDiscovery = new RoslynHandlerDiscovery(
            Known.CoreNamespace,
            includeNamespacePrefix: effectiveOptions.IncludeNamespacePrefix,
            commandContextTypeFqn: effectiveOptions.CommandContextType);

        var discoveryResult = handlerDiscovery.Discover(compilation, handlerSymbols);

        // ---------------------------------------------------------------------
        // 4) Discover semantic UseTinyDispatcher<TContext> calls (facts-only)
        // ---------------------------------------------------------------------
        var useTinyDispatcherInvocations =
            UseTinyDispatcherInvocationExtractor.FindAllUseTinyDispatcherCalls(compilation);

        var allUseTinyDispatcherCalls =
            ctxInference.ResolveAllUseTinyDispatcherContexts(useTinyDispatcherInvocations, compilation);

        // ---------------------------------------------------------------------
        // 5) Extract middleware registrations + referenced policies from bootstrap lambdas (facts-only)
        // ---------------------------------------------------------------------
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

        // ---------------------------------------------------------------------
        // 6) Build policy specs (facts-only)
        // ---------------------------------------------------------------------
        var policies = policyBuilder.Build(compilation, policyTypeSymbols);

        // ---------------------------------------------------------------------
        // 7) Normalize ordering / distinct (normalization only)
        // ---------------------------------------------------------------------
        var globals = ordering.OrderAndDistinctGlobals(globalEntries);
        var perCommand = ordering.BuildPerCommandMap(perCmdEntries);

        // ---------------------------------------------------------------------
        // 8) Build validation context (still facts-only)
        // ---------------------------------------------------------------------
        var vctx = new GeneratorValidationContext.Builder(compilation, discoveryResult, diagnosticsCatalog)
            .WithHostGate(useTinyCallsSyntax, isHost: useTinyCallsSyntax.Length > 0)
            .WithUseTinyDispatcherCalls(allUseTinyDispatcherCalls)
            .WithExpectedContext(expectedContextFqn)
            .WithPipelineConfig(globals, perCommand, policies)
            .Build();

        return new GeneratorAnalysis(
            Compilation: compilation,
            UseTinyCallsSyntax: useTinyCallsSyntax,
            Discovery: discoveryResult,
            EffectiveOptions: effectiveOptions,
            ValidationContext: vctx,
            Globals: globals,
            PerCommand: perCommand,
            Policies: policies);
    }
    private static DiagnosticBag Validate(GeneratorValidationContext vctx)
    {
        var bag = new DiagnosticBag();

        new ContextConsistencyValidator().Validate(vctx, bag);
        new DuplicateHandlerValidator().Validate(vctx, bag);
        new MiddlewareRefShapeValidator().Validate(vctx, bag);
        new PipelineDiagnosticsValidator().Validate(vctx, bag);

        return bag;
    }
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
