#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Analysis;

namespace TinyDispatcher.SourceGen.Generator.Analysis;

internal static class GeneratorSyntaxProviders
{
    public static IncrementalValueProvider<ImmutableArray<INamedTypeSymbol?>> CreateHandlerCandidates(
        SyntaxValueProvider syntaxProvider)
    {
        return syntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax,
                static (context, cancellationToken) =>
                {
                    var node = (ClassDeclarationSyntax)context.Node;
                    return (INamedTypeSymbol?)context.SemanticModel.GetDeclaredSymbol(node, cancellationToken);
                })
            .Collect();
    }

    public static IncrementalValueProvider<ImmutableArray<InvocationExpressionSyntax?>> CreateUseTinyCalls(
        SyntaxValueProvider syntaxProvider)
    {
        var syntax = new UseTinyDispatcherSyntax();

        return syntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is InvocationExpressionSyntax,
                (context, _) =>
                {
                    var invocation = (InvocationExpressionSyntax)context.Node;
                    return syntax.IsUseTinyDispatcherInvocation(invocation) ? invocation : null;
                })
            .Collect();
    }
}
