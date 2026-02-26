using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace TinyDispatcher.SourceGen.Discovery;

internal static class UseTinyDispatcherInvocationExtractor
{
    public static ImmutableArray<InvocationExpressionSyntax> FindAllUseTinyDispatcherCalls(Compilation compilation)
    {
        var builder = ImmutableArray.CreateBuilder<InvocationExpressionSyntax>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            var root = tree.GetRoot();
            foreach (var node in root.DescendantNodes())
            {
                if (node is not InvocationExpressionSyntax inv)
                    continue;

                if (!IsUseTinyDispatcherGenericCall(inv))
                    continue;

                builder.Add(inv);
            }
        }

        return builder.ToImmutable();
    }

    private static bool IsUseTinyDispatcherGenericCall(InvocationExpressionSyntax inv)
    {
        // services.UseTinyDispatcher<TContext>(...)
        if (inv.Expression is MemberAccessExpressionSyntax ma)
        {
            if (ma.Name is GenericNameSyntax g &&
                g.Identifier.ValueText == "UseTinyDispatcher" &&
                g.TypeArgumentList.Arguments.Count == 1)
                return true;

            if (ma.Name is IdentifierNameSyntax id &&
                id.Identifier.ValueText == "UseTinyNoOpContext")
                return true;
        }

        // UseTinyDispatcher<TContext>(...)  (static import / extension form)
        if (inv.Expression is GenericNameSyntax g2)
        {
            if (g2.Identifier.ValueText == "UseTinyDispatcher" &&
                g2.TypeArgumentList.Arguments.Count == 1)
                return true;
        }

        if (inv.Expression is IdentifierNameSyntax id2)
        {
            if (id2.Identifier.ValueText == "UseTinyNoOpContext")
                return true;
        }

        return false;
    }
}