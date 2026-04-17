#nullable enable

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TinyDispatcher.SourceGen.Generator.Models;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen;

public sealed class GeneratorInputTests
{
    [Fact]
    public void Create_removes_null_candidates_and_keeps_generator_inputs()
    {
        var compilation = CSharpCompilation.Create("Tests");
        var handler = CreateTypeSymbol(compilation);
        var invocation = ParseInvocation("services.UseTinyDispatcher<MyApp.AppContext>(_ => { })");
        var options = EmptyAnalyzerConfigOptionsProvider.Instance;

        var input = GeneratorInput.Create(
            compilation,
            ImmutableArray.Create<INamedTypeSymbol?>(null, handler, null),
            ImmutableArray.Create<InvocationExpressionSyntax?>(null, invocation, null),
            options);

        Assert.Same(compilation, input.Compilation);
        Assert.Same(options, input.Options);
        Assert.Equal(handler, Assert.Single(input.HandlerSymbols), SymbolEqualityComparer.Default);
        Assert.Same(invocation, Assert.Single(input.UseTinyCallsSyntax));
    }

    private static INamedTypeSymbol CreateTypeSymbol(CSharpCompilation compilation)
    {
        var tree = CSharpSyntaxTree.ParseText("namespace MyApp { public sealed class Handler { } }");
        var typedCompilation = compilation.AddSyntaxTrees(tree);
        var declaration = tree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Single();

        return Assert.IsAssignableFrom<INamedTypeSymbol>(
            typedCompilation.GetSemanticModel(tree).GetDeclaredSymbol(declaration));
    }

    private static InvocationExpressionSyntax ParseInvocation(string expression)
    {
        return Assert.IsType<InvocationExpressionSyntax>(SyntaxFactory.ParseExpression(expression));
    }

}
