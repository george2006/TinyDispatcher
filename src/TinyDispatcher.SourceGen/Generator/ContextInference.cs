using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

        for (int i = 0; i < useTinyDispatcherInvocations.Length; i++)
        {
            var inv = useTinyDispatcherInvocations[i];

            GenericNameSyntax? g = null;

            if (inv.Expression is MemberAccessExpressionSyntax ma)
                g = ma.Name as GenericNameSyntax;
            else
                g = inv.Expression as GenericNameSyntax;

            if (g is null || g.TypeArgumentList.Arguments.Count != 1)
                continue;

            var ctxSyntax = g.TypeArgumentList.Arguments[0];
            var model = compilation.GetSemanticModel(inv.SyntaxTree);
            var ctxType = model.GetTypeInfo(ctxSyntax).Type;
            if (ctxType is null)
                continue;

            var fqn = Fqn.EnsureGlobal(ctxType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            builder.Add(new UseTinyDispatcherCall(fqn, inv.GetLocation()));
        }

        return builder.ToImmutable();
    }

    public string? TryInferContextTypeFromUseTinyCalls(
        ImmutableArray<InvocationExpressionSyntax> useTinyDispatcherInvocations,
        Compilation compilation)
    {
        var all = ResolveAllUseTinyDispatcherContexts(useTinyDispatcherInvocations, compilation);
        if (all.IsDefaultOrEmpty) return null;
        return all[0].ContextTypeFqn;
    }
}
