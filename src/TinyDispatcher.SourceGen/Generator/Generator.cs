#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Diagnostics;
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
            data.Options);

        var validation = new GeneratorValidationPhase().Validate(analysis, diagnosticsCatalog);

        if (ReportAndHasErrors(roslyn, validation.Diagnostics))
            return;

        new GeneratorGenerationPhase().Generate(roslyn, analysis, validation);
    }

    private static ImmutableArray<INamedTypeSymbol> NormalizeHandlerSymbols(
        ImmutableArray<INamedTypeSymbol?> handlers)
    {
        if (handlers.IsDefaultOrEmpty)
            return ImmutableArray<INamedTypeSymbol>.Empty;

        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>(handlers.Length);

        for (var i = 0; i < handlers.Length; i++)
        {
            var handler = handlers[i];
            if (handler is not null)
                builder.Add(handler);
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<InvocationExpressionSyntax> NormalizeUseTinyCalls(
        ImmutableArray<InvocationExpressionSyntax?> useTinyCalls)
    {
        if (useTinyCalls.IsDefaultOrEmpty)
            return ImmutableArray<InvocationExpressionSyntax>.Empty;

        var builder = ImmutableArray.CreateBuilder<InvocationExpressionSyntax>(useTinyCalls.Length);

        for (var i = 0; i < useTinyCalls.Length; i++)
        {
            var useTinyCall = useTinyCalls[i];
            if (useTinyCall is not null)
                builder.Add(useTinyCall);
        }

        return builder.ToImmutable();
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
}
