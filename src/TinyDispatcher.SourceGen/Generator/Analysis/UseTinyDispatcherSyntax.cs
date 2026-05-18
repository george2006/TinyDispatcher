using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;

namespace TinyDispatcher.SourceGen.Generator.Analysis;

internal sealed class UseTinyDispatcherSyntax
{
    private const string UseTinyDispatcherMethodName = "UseTinyDispatcher";
    private const string UseTinyNoOpContextMethodName = "UseTinyNoOpContext";

    public bool IsBootstrapInvocation(InvocationExpressionSyntax invocation)
    {
        // services.UseTinyDispatcher<TContext>(...)
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var isDispatcherBootstrapCall = IsUseTinyDispatcherCall(memberAccess.Name);
            if (isDispatcherBootstrapCall)
            {
                return true;
            }

            var isNoOpBootstrapCall = IsUseTinyNoOpContextCall(memberAccess.Name);
            if (isNoOpBootstrapCall)
            {
                return true;
            }

            return false;
        }

        // UseTinyDispatcher<TContext>(...) (using static)
        var isStaticDispatcherBootstrapCall = IsUseTinyDispatcherCall(invocation.Expression);
        if (isStaticDispatcherBootstrapCall)
        {
            return true;
        }

        var isStaticNoOpBootstrapCall = IsUseTinyNoOpContextCall(invocation.Expression);
        if (isStaticNoOpBootstrapCall)
        {
            return true;
        }

        return false;
    }

    private static bool IsUseTinyDispatcherCall(ExpressionSyntax expression)
    {
        if (expression is not GenericNameSyntax genericName)
        {
            return false;
        }

        var hasUseTinyDispatcherName = string.Equals(
            genericName.Identifier.ValueText,
            UseTinyDispatcherMethodName,
            StringComparison.Ordinal);

        if (!hasUseTinyDispatcherName)
        {
            return false;
        }

        return genericName.TypeArgumentList != null &&
               genericName.TypeArgumentList.Arguments.Count == 1;
    }

    private static bool IsUseTinyNoOpContextCall(ExpressionSyntax expression)
    {
        if (expression is not IdentifierNameSyntax identifierName)
        {
            return false;
        }

        return string.Equals(
            identifierName.Identifier.ValueText,
            UseTinyNoOpContextMethodName,
            StringComparison.Ordinal);
    }
}
