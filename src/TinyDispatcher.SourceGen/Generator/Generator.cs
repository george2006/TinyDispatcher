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
using TinyDispatcher.SourceGen.Internal;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Discovery;

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

        // Filter nullables (broken/partial code scenarios).
        var handlerSymbols = data.Left.Left.Handlers
            .Where(static s => s != null)
            .Select(static s => s!)
            .ToImmutableArray();

        var useTinyCalls = data.Left.UseTinyCalls
            .Where(static x => x != null)
            .Select(static x => x!)
            .ToImmutableArray();

        var roslynContext = new RoslynGeneratorContext(spc);

        // Compose components (explicit `new`, no DI container)
        var diagsCatalog = new DiagnosticsCatalog();
        var optionsFactory = new GeneratorOptionsFactory(new OptionsProvider());
        var ctxInference = new ContextInference();
        var mwFactory = new MiddlewareRefFactory(diagsCatalog);
        var extractor = new TinyBootstrapInvocationExtractor(mwFactory);
        var policyBuilder = new PolicySpecBuilder(mwFactory);
        var ordering = new MiddlewareOrdering();

        // Base options
        var baseOptions = optionsFactory.Create(compilation, data.Options);

        // Discover handlers
        var discovery = new RoslynHandlerDiscovery(
            Known.CoreNamespace,
            baseOptions.IncludeNamespacePrefix,
            baseOptions.CommandContextType);

        var discoveryResult = discovery.Discover(compilation, handlerSymbols);

        var dupValidator = new DuplicateHandlerValidator(diagsCatalog);
        var dupDiags = dupValidator.Validate(discoveryResult);

        if (!dupDiags.IsDefaultOrEmpty)
        {
            foreach (var d in dupDiags)
                roslynContext.ReportDiagnostic(d);

            // Stop generation on errors
            if (dupDiags.Any(x => x.Severity == DiagnosticSeverity.Error))
                return;
        }

        // -----------------------------------------------------------------
        // Infer CommandContextType from UseTinyDispatcher<TContext> (SYNTAX-based)
        // -----------------------------------------------------------------
        var inferredCtx = ctxInference.TryInferContextTypeFromUseTinyCalls(useTinyCalls, compilation);
        var effectiveOptions = optionsFactory.ApplyInferredContextIfMissing(baseOptions, inferredCtx);

        // If still no ctx, we cannot generate pipelines (PipelineEmitter requires closed ctx)
        if (string.IsNullOrWhiteSpace(effectiveOptions.CommandContextType))
            return;

        var expectedContextFqn = Fqn.EnsureGlobal(effectiveOptions.CommandContextType!);


        // Always emit contribution + module initializer (+ empty pipeline contribution)
        new ModuleInitializerEmitter().Emit(roslynContext, discoveryResult, effectiveOptions);
        new EmptyPipelineContributionEmitter().Emit(roslynContext, discoveryResult, effectiveOptions);
        // always emits (empty if disabled)
        new HandlerRegistrationsEmitter().Emit(roslynContext, discoveryResult, effectiveOptions);

        // -----------------------------------------------------------------
        // Middleware + Policy discovery from TinyBootstrap fluent calls
        // -----------------------------------------------------------------
        var globalEntries = new List<OrderedEntry>();
        var perCmdEntries = new List<OrderedPerCommandEntry>();
        var policyTypeSymbols = new List<INamedTypeSymbol>();
        var diags = new List<Diagnostic>();

        for (var i = 0; i < useTinyCalls.Length; i++)
        {
            extractor.Extract(
                useTinyCalls[i],
                compilation,
                expectedContextFqn,
                globalEntries,
                perCmdEntries,
                policyTypeSymbols,
                diags);
        }

        // -----------------------------------------------------------------
        // HOST GATE:
        // If no UseTinyDispatcher calls in this project → do not emit pipelines.
        // -----------------------------------------------------------------
        if (useTinyCalls.IsDefaultOrEmpty || useTinyCalls.Length == 0)
            return;

        // Policies: build PolicySpec map (policyTypeFqn -> PolicySpec)
        var policies = policyBuilder.Build(compilation, expectedContextFqn, policyTypeSymbols, diags);

        if (diags.Count > 0)
        {
            for (var i = 0; i < diags.Count; i++)
                roslynContext.ReportDiagnostic(diags[i]);
            return;
        }

        // Order + distinct middleware
        var globals = ordering.OrderAndDistinctGlobals(globalEntries);
        var perCmd = ordering.BuildPerCommandMap(perCmdEntries);

        // If nothing at all, skip
        var hasAny =
            globals.Length > 0 ||
            perCmd.Count > 0 ||
            policies.Count > 0;

        if (!hasAny)
            return;

        // Emit pipelines
        new PipelineEmitter(globals, perCmd, policies)
            .Emit(roslynContext, discoveryResult, effectiveOptions);
    }
}
