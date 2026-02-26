#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator;

internal sealed class ContextInference
{
    public ImmutableArray<UseTinyDispatcherCall> ResolveAllUseTinyDispatcherContexts(
        ImmutableArray<InvocationExpressionSyntax> useTinyDispatcherInvocations,
        Compilation compilation)
    {
        if (useTinyDispatcherInvocations.IsDefaultOrEmpty)
            return ImmutableArray<UseTinyDispatcherCall>.Empty;

        var builder = ImmutableArray.CreateBuilder<UseTinyDispatcherCall>();

        for (var i = 0; i < useTinyDispatcherInvocations.Length; i++)
        {
            var inv = useTinyDispatcherInvocations[i];

            if (IsUseTinyNoOpContextCall(inv))
            {
                builder.Add(new UseTinyDispatcherCall("global::TinyDispatcher.Context.NoOpContext", inv.GetLocation()));
                continue;
            }

            GenericNameSyntax? g = null;

            if (inv.Expression is MemberAccessExpressionSyntax ma)
                g = ma.Name as GenericNameSyntax;
            else
                g = inv.Expression as GenericNameSyntax;

            if (g is null)
                continue;

            if (g.TypeArgumentList.Arguments.Count != 1)
                continue;

            var ctxSyntax = g.TypeArgumentList.Arguments[0];

            var model = compilation.GetSemanticModel(inv.SyntaxTree);
            var ctxType = model.GetTypeInfo(ctxSyntax).Type;

            if (ctxType is null)
                continue;

            if (ctxType is ITypeParameterSymbol)
                continue;

            if (ctxType is IErrorTypeSymbol)
                continue;

            var ctxFqn = Fqn.EnsureGlobal(
                ctxType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

            builder.Add(new UseTinyDispatcherCall(ctxFqn, inv.GetLocation()));
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
        if (all.IsDefaultOrEmpty)
            return null;

        return all[0].ContextTypeFqn;
    }

    private static bool IsUseTinyNoOpContextCall(InvocationExpressionSyntax inv)
    {
        if (inv.Expression is MemberAccessExpressionSyntax ma &&
            ma.Name is IdentifierNameSyntax id &&
            id.Identifier.ValueText == "UseTinyNoOpContext")
            return true;

        if (inv.Expression is IdentifierNameSyntax id2 &&
            id2.Identifier.ValueText == "UseTinyNoOpContext")
            return true;

        return false;
    }
}