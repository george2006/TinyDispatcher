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

        // ---------------------------------------------------------------------
        // Components (facts-only discovery; diagnostics only in VALIDATE)
        // ---------------------------------------------------------------------
        var diagnosticsCatalog = new DiagnosticsCatalog();
        var invocationExtractor = new TinyBootstrapInvocationExtractor(); // facts-only
        var policyBuilder = new PolicySpecBuilder();                     // facts-only
        var ordering = new MiddlewareOrdering();
        var ctxInference = new ContextInference();
        var optionsFactory = new GeneratorOptionsFactory(new OptionsProvider());

        // ---------------------------------------------------------------------
        // DISCOVER (NO diagnostics here)
        // ---------------------------------------------------------------------

        // 1) Read options
        var baseOptions = optionsFactory.Create(compilation, data.Options);

        // 2) Infer context from syntax-discovered UseTinyDispatcher calls.
        //    (ContextInference ignores type parameters so we never infer "TContext".)
        var inferredCtxFqn = ctxInference.TryInferContextTypeFromUseTinyCalls(useTinyCallsSyntax, compilation);
        var effectiveOptions = optionsFactory.ApplyInferredContextIfMissing(baseOptions, inferredCtxFqn);

        var expectedContextFqn =
            string.IsNullOrWhiteSpace(effectiveOptions.CommandContextType)
                ? string.Empty
                : Fqn.EnsureGlobal(effectiveOptions.CommandContextType!);

        // 3) Discover handlers (configured with the effective context)
        var handlerDiscovery = new RoslynHandlerDiscovery(
            Known.CoreNamespace,
            includeNamespacePrefix: effectiveOptions.IncludeNamespacePrefix,
            commandContextTypeFqn: effectiveOptions.CommandContextType);

        var discoveryResult = handlerDiscovery.Discover(compilation, handlerSymbols);

        // 4) Discover semantic UseTinyDispatcher<TContext> calls (facts-only)
        var useTinyDispatcherInvocations =
            UseTinyDispatcherInvocationExtractor.FindAllUseTinyDispatcherCalls(compilation);

        var allUseTinyDispatcherCalls =
            ctxInference.ResolveAllUseTinyDispatcherContexts(useTinyDispatcherInvocations, compilation);

        // 5) Discover middleware registrations + policies referenced from bootstrap lambda(s) (facts-only)
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

        // 6) Build policy specs (facts-only)
        var policies = policyBuilder.Build(compilation, policyTypeSymbols);

        // 7) Normalize ordering / distinct (normalization only)
        var globals = ordering.OrderAndDistinctGlobals(globalEntries);
        var perCommand = ordering.BuildPerCommandMap(perCmdEntries);

        // ---------------------------------------------------------------------
        // BUILD VALIDATION CONTEXT
        // ---------------------------------------------------------------------
        var vctx = new GeneratorValidationContext.Builder(compilation, discoveryResult, diagnosticsCatalog)
            .WithHostGate(useTinyCallsSyntax, isHost: useTinyCallsSyntax.Length > 0)
            .WithUseTinyDispatcherCalls(allUseTinyDispatcherCalls)
            .WithExpectedContext(expectedContextFqn)
            .WithPipelineConfig(globals, perCommand, policies)
            .Build();

        // ---------------------------------------------------------------------
        // VALIDATE (ALL diagnostics produced here)
        // ---------------------------------------------------------------------
        var bag = new DiagnosticBag();

        // Establish host/context rules (canonical validator for DISP110/111)
        new ContextConsistencyValidator().Validate(vctx, bag);

        // Handlers
        new DuplicateHandlerValidator().Validate(vctx, bag);
        // new MissingHandlerValidator().Validate(vctx, bag); // optional

        // Middleware refs
        new MiddlewareRefShapeValidator().Validate(vctx, bag);

        // Pipelines / policies
        new PipelineDiagnosticsValidator().Validate(vctx, bag);
        // new PolicySpecValidator().Validate(vctx, bag); // optional

        if (ReportAndHasErrors(roslyn, bag))
            return;

        // ---------------------------------------------------------------------
        // EMIT (only after validation passes)
        // ---------------------------------------------------------------------

        // IMPORTANT:
        // Emitters must use the validated context (vctx.ExpectedContextFqn), not a potentially
        // unvalidated context from options. This prevents emitting "TContext" into generated code.
        var emitOptions =
            string.IsNullOrWhiteSpace(vctx.ExpectedContextFqn)
                ? effectiveOptions
                : new GeneratorOptions(
                    GeneratedNamespace: effectiveOptions.GeneratedNamespace,
                    EmitDiExtensions: effectiveOptions.EmitDiExtensions,
                    EmitHandlerRegistrations: effectiveOptions.EmitHandlerRegistrations,
                    IncludeNamespacePrefix: effectiveOptions.IncludeNamespacePrefix,
                    CommandContextType: vctx.ExpectedContextFqn,
                    EmitPipelineMap: effectiveOptions.EmitPipelineMap,
                    PipelineMapFormat: effectiveOptions.PipelineMapFormat);

        // Always emit baseline artifacts (safe)
        new ModuleInitializerEmitter().Emit(roslyn, discoveryResult, emitOptions);
        new EmptyPipelineContributionEmitter().Emit(roslyn, discoveryResult, emitOptions);
        new HandlerRegistrationsEmitter().Emit(roslyn, discoveryResult, emitOptions);

        // Host gate: no UseTinyDispatcher(...) calls => no host artifacts
        if (!vctx.IsHostProject)
            return;

        // Need a closed validated context to emit pipelines
        if (string.IsNullOrWhiteSpace(vctx.ExpectedContextFqn))
            return;

        // If there are no pipeline contributions, skip pipeline emission
        var hasAnyPipelineContributions =
            vctx.Globals.Length > 0 ||
            vctx.PerCommand.Count > 0 ||
            vctx.Policies.Count > 0;

        if (!hasAnyPipelineContributions)
            return;

        new PipelineEmitter(vctx.Globals, vctx.PerCommand, vctx.Policies)
            .Emit(roslyn, discoveryResult, emitOptions);
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
}
