using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator;

internal sealed class ContextInference
{
    public string? TryInferContextTypeFromUseTinyCalls(
        ImmutableArray<InvocationExpressionSyntax> calls,
        Compilation compilation)
    {
        for (var i = 0; i < calls.Length; i++)
        {
            var call = calls[i];

            GenericNameSyntax? g = null;

            if (call.Expression is MemberAccessExpressionSyntax ma)
                g = ma.Name as GenericNameSyntax;
            else
                g = call.Expression as GenericNameSyntax;

            if (g == null || g.TypeArgumentList == null || g.TypeArgumentList.Arguments.Count != 1)
                continue;

            var ctxTypeSyntax = g.TypeArgumentList.Arguments[0];

            var model = compilation.GetSemanticModel(call.SyntaxTree);
            var ctxType = model.GetTypeInfo(ctxTypeSyntax).Type;

            if (ctxType == null)
                continue;

            return Fqn.EnsureGlobal(ctxType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }

        return null;
    }
}

