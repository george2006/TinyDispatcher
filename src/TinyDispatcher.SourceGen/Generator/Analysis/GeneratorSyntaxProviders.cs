#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Analysis;

namespace TinyDispatcher.SourceGen.Generator.Analysis;

internal static class GeneratorSyntaxProviders
{
    private static ImmutableHashSet<string> HandlerContractNames { get; } =
        ImmutableHashSet.Create(StringComparer.Ordinal, "ICommandHandler", "IQueryHandler");

    public static IncrementalValueProvider<ImmutableArray<INamedTypeSymbol?>> CreateHandlerCandidates(
        SyntaxValueProvider syntaxProvider)
    {
        return syntaxProvider
            .CreateSyntaxProvider(
                static (node, _) =>
                {
                    if (node is not ClassDeclarationSyntax classDecl)
                        return false;

                    return IsHandlerCandidate(classDecl);
                },
                static (context, cancellationToken) =>
                {
                    var node = (ClassDeclarationSyntax)context.Node;
                    return (INamedTypeSymbol?)context.SemanticModel.GetDeclaredSymbol(node, cancellationToken);
                })
            .Collect();
    }

    private static bool IsHandlerCandidate(ClassDeclarationSyntax classDecl)
    {
        var baseTypes = classDecl.BaseList?.Types;
        if (baseTypes is null)
        {
            return false;
        }

        foreach (var baseType in baseTypes)
        {
            var contractName = GetImplementedContractName(baseType.Type);
            if (contractName is not null && HandlerContractNames.Contains(contractName))
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetImplementedContractName(TypeSyntax type)
    {
        var contractTypeName = GetTypeNameWithoutNamespace(type);
        return contractTypeName?.Identifier.ValueText;
    }

    private static SimpleNameSyntax? GetTypeNameWithoutNamespace(TypeSyntax type)
    {
        switch (type)
        {
            case SimpleNameSyntax simpleName:
                return simpleName;
            case QualifiedNameSyntax qualifiedName:
                return GetTypeNameWithoutNamespace(qualifiedName.Right);
            case AliasQualifiedNameSyntax aliasQualifiedName:
                return GetTypeNameWithoutNamespace(aliasQualifiedName.Name);
            default:
                return null;
        }
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
                    return syntax.IsBootstrapInvocation(invocation) ? invocation : null;
                })
            .Collect();
    }
}
