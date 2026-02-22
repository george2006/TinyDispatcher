#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;
using TinyDispatcher.SourceGen.Abstractions;
using TinyDispatcher.SourceGen.Emitters.Handlers;
using TinyDispatcher.SourceGen.Emitters.ModuleInitializer;
using TinyDispatcher.SourceGen.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Internal;
using TinyDispatcher.SourceGen.Validation;

namespace TinyDispatcher.SourceGen.Generator;

[Generator]
public sealed class Generator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var syntax = new UseTinyDispatcherSyntax();

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

        var analysis = GeneratorAnalyzer.Analyze(
            compilation,
            handlerSymbols,
            useTinyCallsSyntax,
            data.Options,
            diagnosticsCatalog);

        var bag = GeneratorValidator.Validate(analysis.ValidationContext);

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