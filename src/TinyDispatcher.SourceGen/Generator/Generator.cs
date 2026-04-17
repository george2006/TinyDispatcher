#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Diagnostics;
using TinyDispatcher.SourceGen.Generator.Models;

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
        var input = GeneratorInput.Create(
            data.Left.Left.Compilation,
            data.Left.Left.Handlers,
            data.Left.UseTinyCalls,
            data.Options);

        var roslyn = new RoslynGeneratorContext(spc);
        var diagnosticsCatalog = new DiagnosticsCatalog();

        var analysis = GeneratorAnalysisPhase.Analyze(
            input.Compilation,
            input.UseTinyCallsSyntax,
            input.Options);

        var extraction = new GeneratorExtractionPhase().Extract(analysis, input.HandlerSymbols);
        var validation = new GeneratorValidationPhase().Validate(analysis, extraction, diagnosticsCatalog);

        if (GeneratorDiagnosticReporter.ReportAndHasErrors(roslyn, validation.Diagnostics))
            return;

        new GeneratorGenerationPhase().Generate(roslyn, analysis, extraction, validation);
    }
}
