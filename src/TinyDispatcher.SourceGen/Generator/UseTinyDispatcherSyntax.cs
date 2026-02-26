using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace TinyDispatcher.SourceGen.Generator;

internal sealed class UseTinyDispatcherSyntax
{
    public bool IsUseTinyDispatcherInvocation(InvocationExpressionSyntax inv)
    {
        // services.UseTinyDispatcher<TContext>(...)
        if (inv.Expression is MemberAccessExpressionSyntax ma)
        {
            if (ma.Name is GenericNameSyntax g &&
                string.Equals(g.Identifier.ValueText, "UseTinyDispatcher", StringComparison.Ordinal) &&
                g.TypeArgumentList != null &&
                g.TypeArgumentList.Arguments.Count == 1)
                return true;

            if (ma.Name is IdentifierNameSyntax id &&
                string.Equals(id.Identifier.ValueText, "UseTinyNoOpContext", StringComparison.Ordinal))
                return true;

            return false;
        }

        // UseTinyDispatcher<TContext>(...) (using static)
        if (inv.Expression is GenericNameSyntax gg &&
            string.Equals(gg.Identifier.ValueText, "UseTinyDispatcher", StringComparison.Ordinal) &&
            gg.TypeArgumentList != null &&
            gg.TypeArgumentList.Arguments.Count == 1)
            return true;

        if (inv.Expression is IdentifierNameSyntax id2 &&
            string.Equals(id2.Identifier.ValueText, "UseTinyNoOpContext", StringComparison.Ordinal))
            return true;

        return false;
    }
}