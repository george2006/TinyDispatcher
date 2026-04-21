#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Analysis;

internal sealed class BootstrapLambdaExtractor
{
    public ImmutableArray<ConfirmedBootstrapLambda> Extract(
        Compilation compilation,
        ImmutableArray<InvocationExpressionSyntax> useTinyCallsSyntax)
    {
        if (useTinyCallsSyntax.IsDefaultOrEmpty)
        {
            return ImmutableArray<ConfirmedBootstrapLambda>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<ConfirmedBootstrapLambda>(useTinyCallsSyntax.Length);

        for (var i = 0; i < useTinyCallsSyntax.Length; i++)
        {
            var useTinyCall = useTinyCallsSyntax[i];
            var semanticModel = compilation.GetSemanticModel(useTinyCall.SyntaxTree);
            var lambda = SelectBootstrapLambda(useTinyCall, semanticModel);

            if (lambda is not null)
            {
                builder.Add(new ConfirmedBootstrapLambda(semanticModel, lambda));
            }
        }

        return builder.ToImmutable();
    }

    private static LambdaExpressionSyntax? SelectBootstrapLambda(
        InvocationExpressionSyntax useTinyCall,
        SemanticModel model)
    {
        if (useTinyCall.ArgumentList is null)
        {
            return null;
        }

        foreach (var arg in useTinyCall.ArgumentList.Arguments)
        {
            if (arg.Expression is not LambdaExpressionSyntax lambda)
            {
                continue;
            }

            if (IsTinyBootstrapDelegate(lambda, model))
            {
                return lambda;
            }
        }

        return SelectFirstLambda(useTinyCall);
    }

    private static LambdaExpressionSyntax? SelectFirstLambda(InvocationExpressionSyntax useTinyCall)
    {
        if (useTinyCall.ArgumentList is null)
        {
            return null;
        }

        foreach (var argument in useTinyCall.ArgumentList.Arguments)
        {
            if (argument.Expression is LambdaExpressionSyntax lambda)
            {
                return lambda;
            }
        }

        return null;
    }

    private static bool IsTinyBootstrapDelegate(LambdaExpressionSyntax lambda, SemanticModel model)
    {
        var converted = model.GetTypeInfo(lambda).ConvertedType as INamedTypeSymbol;
        var invoke = converted?.DelegateInvokeMethod;
        if (invoke is null)
        {
            return false;
        }

        if (invoke.Parameters.Length != 1)
        {
            return false;
        }

        var parameterType = invoke.Parameters[0].Type;

        if (parameterType.Name == "TinyBootstrap")
        {
            return true;
        }

        var fqn = Fqn.FromType(parameterType);
        return string.Equals(fqn, "global::TinyDispatcher.TinyBootstrap", System.StringComparison.Ordinal);
    }
}
