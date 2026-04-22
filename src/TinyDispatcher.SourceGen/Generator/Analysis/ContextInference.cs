#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
    /// Returns null if none can be inferred safely.
    /// </summary>
    public string? TryInferContextTypeFromUseTinyCalls(
        ImmutableArray<InvocationExpressionSyntax> useTinyCallsSyntax,
        Compilation compilation)
    {
        // We resolve using the same logic as the semantic resolver, but accept the syntax list.
        var all = ResolveAllUseTinyDispatcherContexts(useTinyCallsSyntax, compilation);
        return TryInferContextTypeFromResolvedCalls(all);
    }

    public string? TryInferContextTypeFromResolvedCalls(
        ImmutableArray<UseTinyDispatcherCall> useTinyDispatcherCalls)
    {
        if (useTinyDispatcherCalls.IsDefaultOrEmpty)
        {
            return null;
        }

        return useTinyDispatcherCalls[0].ContextTypeFqn;
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
