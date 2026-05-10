#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Analysis;

internal sealed class ContextInference
{
    public ImmutableArray<UseTinyDispatcherCall> ResolveAllUseTinyDispatcherContexts(
        ImmutableArray<InvocationExpressionSyntax> useTinyDispatcherInvocations,
        Compilation compilation)
    {
        if (useTinyDispatcherInvocations.IsDefaultOrEmpty)
        {
            return ImmutableArray<UseTinyDispatcherCall>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<UseTinyDispatcherCall>();

        for (var i = 0; i < useTinyDispatcherInvocations.Length; i++)
        {
            var invocation = useTinyDispatcherInvocations[i];
            if (TryResolveUseTinyDispatcherCall(invocation, compilation, out var useTinyDispatcherCall))
            {
                builder.Add(useTinyDispatcherCall);
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Best-effort inference of a concrete context from syntax-discovered UseTinyDispatcher calls.
    /// Succeeds only when all resolved calls agree on one context.
    /// </summary>
    public bool TryInferSingleContextTypeFromUseTinyCalls(
        ImmutableArray<InvocationExpressionSyntax> useTinyCallsSyntax,
        Compilation compilation,
        out string contextTypeFqn)
    {
        // We resolve using the same logic as the semantic resolver, but accept the syntax list.
        var all = ResolveAllUseTinyDispatcherContexts(useTinyCallsSyntax, compilation);
        return TryInferSingleContextTypeFromResolvedCalls(all, out contextTypeFqn);
    }

    public bool TryInferSingleContextTypeFromResolvedCalls(
        ImmutableArray<UseTinyDispatcherCall> useTinyDispatcherCalls,
        out string contextTypeFqn)
    {
        contextTypeFqn = string.Empty;

        if (useTinyDispatcherCalls.IsDefaultOrEmpty)
        {
            return false;
        }

        contextTypeFqn = useTinyDispatcherCalls[0].ContextTypeFqn;

        for (var i = 1; i < useTinyDispatcherCalls.Length; i++)
        {
            var isSameContext = string.Equals(
                contextTypeFqn,
                useTinyDispatcherCalls[i].ContextTypeFqn,
                StringComparison.Ordinal);

            if (!isSameContext)
            {
                contextTypeFqn = string.Empty;
                return false;
            }
        }

        return true;
    }

    public bool TryResolveUseTinyDispatcherContext(
        InvocationExpressionSyntax invocation,
        Compilation compilation,
        out UseTinyDispatcherCall useTinyDispatcherCall)
    {
        return TryResolveUseTinyDispatcherCall(invocation, compilation, out useTinyDispatcherCall);
    }

    private static bool TryResolveUseTinyDispatcherCall(
        InvocationExpressionSyntax invocation,
        Compilation compilation,
        out UseTinyDispatcherCall useTinyDispatcherCall)
    {
        if (TryCreateNoOpContextCall(invocation, out useTinyDispatcherCall))
        {
            return true;
        }

        return TryResolveGenericContextCall(invocation, compilation, out useTinyDispatcherCall);
    }

    private static bool TryCreateNoOpContextCall(
        InvocationExpressionSyntax invocation,
        out UseTinyDispatcherCall useTinyDispatcherCall)
    {
        useTinyDispatcherCall = default;

        var isNoOpContextCall = IsUseTinyNoOpContextCall(invocation);
        if (!isNoOpContextCall)
        {
            return false;
        }

        useTinyDispatcherCall = new UseTinyDispatcherCall(
            "global::TinyDispatcher.Context.NoOpContext",
            invocation.GetLocation());
        return true;
    }

    private static bool TryResolveGenericContextCall(
        InvocationExpressionSyntax invocation,
        Compilation compilation,
        out UseTinyDispatcherCall useTinyDispatcherCall)
    {
        useTinyDispatcherCall = default;

        var genericName = TryGetUseTinyDispatcherGenericName(invocation);
        if (genericName is null)
        {
            return false;
        }

        var hasSingleTypeArgument = genericName.TypeArgumentList.Arguments.Count == 1;
        if (!hasSingleTypeArgument)
        {
            return false;
        }

        var contextTypeSyntax = genericName.TypeArgumentList.Arguments[0];
        var semanticModel = compilation.GetSemanticModel(invocation.SyntaxTree);
        var contextType = semanticModel.GetTypeInfo(contextTypeSyntax).Type;
        var hasSupportedContextType = IsSupportedContextType(contextType);

        if (!hasSupportedContextType)
        {
            return false;
        }

        useTinyDispatcherCall = new UseTinyDispatcherCall(
            Fqn.FromType(contextType!),
            invocation.GetLocation());
        return true;
    }

    private static GenericNameSyntax? TryGetUseTinyDispatcherGenericName(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name as GenericNameSyntax;
        }

        return invocation.Expression as GenericNameSyntax;
    }

    private static bool IsSupportedContextType(ITypeSymbol? contextType)
    {
        if (contextType is null)
        {
            return false;
        }

        if (contextType is ITypeParameterSymbol)
        {
            return false;
        }

        return contextType is not IErrorTypeSymbol;
    }

    private static bool IsUseTinyNoOpContextCall(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name is IdentifierNameSyntax memberName &&
            memberName.Identifier.ValueText == "UseTinyNoOpContext")
        {
            return true;
        }

        if (invocation.Expression is IdentifierNameSyntax identifierName &&
            identifierName.Identifier.ValueText == "UseTinyNoOpContext")
        {
            return true;
        }

        return false;
    }
}
