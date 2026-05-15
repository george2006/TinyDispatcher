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
        ImmutableArray<InvocationExpressionSyntax> confirmedBootstrapCalls)
    {
        if (confirmedBootstrapCalls.IsDefaultOrEmpty)
        {
            return ImmutableArray<ConfirmedBootstrapLambda>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<ConfirmedBootstrapLambda>(confirmedBootstrapCalls.Length);
        var contextInference = new ContextInference();

        for (var i = 0; i < confirmedBootstrapCalls.Length; i++)
        {
            var bootstrapCall = confirmedBootstrapCalls[i];
            var semanticModel = compilation.GetSemanticModel(bootstrapCall.SyntaxTree);
            var lambda = SelectBootstrapLambda(bootstrapCall, semanticModel);
            var hasBootstrapLambda = lambda is not null;
            var hasResolvedContext = contextInference.TryResolveBootstrapContext(
                bootstrapCall,
                compilation,
                out var resolvedCall);

            if (hasBootstrapLambda && hasResolvedContext)
            {
                builder.Add(new ConfirmedBootstrapLambda(semanticModel, lambda!, resolvedCall.ContextTypeFqn));
            }
        }

        return builder.ToImmutable();
    }

    private static LambdaExpressionSyntax? SelectBootstrapLambda(
        InvocationExpressionSyntax bootstrapCall,
        SemanticModel model)
    {
        if (bootstrapCall.ArgumentList is null)
        {
            return null;
        }

        foreach (var arg in bootstrapCall.ArgumentList.Arguments)
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

        return SelectFirstLambda(bootstrapCall);
    }

    private static LambdaExpressionSyntax? SelectFirstLambda(InvocationExpressionSyntax bootstrapCall)
    {
        if (bootstrapCall.ArgumentList is null)
        {
            return null;
        }

        foreach (var argument in bootstrapCall.ArgumentList.Arguments)
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
