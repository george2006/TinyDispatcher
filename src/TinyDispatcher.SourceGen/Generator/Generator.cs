#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Analysis;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator;

[Generator]
public sealed class Generator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var handlerCandidates = GeneratorSyntaxProviders.CreateHandlerCandidates(context.SyntaxProvider);
        var useTinyCalls = GeneratorSyntaxProviders.CreateUseTinyCalls(context.SyntaxProvider);

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
        new GeneratorPipeline().Execute(roslyn, input);
    }
}
