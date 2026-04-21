#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Analysis;

internal sealed class UseTinyDispatcherSemanticFilter
{
    private const string ServiceCollectionFqn = "global::Microsoft.Extensions.DependencyInjection.IServiceCollection";
    private const string TinyDispatcherNamespace = "TinyDispatcher";
    private const string DependencyInjectionNamespace = "Microsoft.Extensions.DependencyInjection";
    private const string UseTinyDispatcherMethodName = "UseTinyDispatcher";
    private const string UseTinyNoOpContextMethodName = "UseTinyNoOpContext";

    public ImmutableArray<InvocationExpressionSyntax> Filter(
        Compilation compilation,
        ImmutableArray<InvocationExpressionSyntax> candidates)
    {
        if (candidates.IsDefaultOrEmpty)
        {
            return ImmutableArray<InvocationExpressionSyntax>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<InvocationExpressionSyntax>(candidates.Length);

        for (var i = 0; i < candidates.Length; i++)
        {
            var candidate = candidates[i];
            var model = compilation.GetSemanticModel(candidate.SyntaxTree);

            if (IsTinyDispatcherBootstrap(candidate, model))
            {
                builder.Add(candidate);
            }
        }

        return builder.ToImmutable();
    }

    private static bool IsTinyDispatcherBootstrap(
        InvocationExpressionSyntax invocation,
        SemanticModel model)
    {
        var symbolInfo = model.GetSymbolInfo(invocation);

        if (symbolInfo.Symbol is IMethodSymbol method &&
            IsTinyDispatcherBootstrapMethod(method))
        {
            return true;
        }

        for (var i = 0; i < symbolInfo.CandidateSymbols.Length; i++)
        {
            if (symbolInfo.CandidateSymbols[i] is IMethodSymbol candidate &&
                IsTinyDispatcherBootstrapMethod(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTinyDispatcherBootstrapMethod(IMethodSymbol method)
    {
        var original = method.ReducedFrom ?? method;
        var methodName = original.Name;

        if (!IsKnownBootstrapNamespace(original.ContainingNamespace))
        {
            return false;
        }

        if (!IsServiceCollectionExtension(original))
        {
            return false;
        }

        if (string.Equals(methodName, UseTinyDispatcherMethodName, StringComparison.Ordinal))
        {
            return original.TypeArguments.Length == 1 ||
                   original.TypeParameters.Length == 1;
        }

        return string.Equals(methodName, UseTinyNoOpContextMethodName, StringComparison.Ordinal) &&
               original.TypeArguments.Length == 0 &&
               original.TypeParameters.Length == 0;
    }

    private static bool IsServiceCollectionExtension(IMethodSymbol method)
    {
        if (method.Parameters.Length == 0)
        {
            return false;
        }

        var firstParameterType = Fqn.FromType(method.Parameters[0].Type);

        return string.Equals(firstParameterType, ServiceCollectionFqn, StringComparison.Ordinal);
    }

    private static bool IsKnownBootstrapNamespace(INamespaceSymbol? containingNamespace)
    {
        var namespaceName = containingNamespace?.ToDisplayString();

        return string.Equals(namespaceName, TinyDispatcherNamespace, StringComparison.Ordinal) ||
               string.Equals(namespaceName, DependencyInjectionNamespace, StringComparison.Ordinal);
    }
}
