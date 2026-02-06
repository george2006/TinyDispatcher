using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;
using TinyDispatcher.SourceGen.Generator;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Discovery;

internal sealed class TinyBootstrapInvocationExtractor
{
    private readonly MiddlewareRefFactory _mwFactory;

    public TinyBootstrapInvocationExtractor(MiddlewareRefFactory mwFactory)
        => _mwFactory = mwFactory ?? throw new ArgumentNullException(nameof(mwFactory));

    public void Extract(
        InvocationExpressionSyntax useTinyCall,
        Compilation compilation,
        string expectedContextFqn,
        List<OrderedEntry> globals,
        List<OrderedPerCommandEntry> perCmd,
        List<INamedTypeSymbol> policies,
        List<Diagnostic> diags)
    {
        if (useTinyCall.ArgumentList is null)
            return;

        var lambda = useTinyCall.ArgumentList.Arguments
            .Select(a => a.Expression)
            .OfType<LambdaExpressionSyntax>()
            .FirstOrDefault();

        if (lambda is null)
            return;

        var model = compilation.GetSemanticModel(useTinyCall.SyntaxTree);

        IEnumerable<InvocationExpressionSyntax> invocations;
        if (lambda.Body is BlockSyntax block)
            invocations = block.DescendantNodes().OfType<InvocationExpressionSyntax>();
        else if (lambda.Body is ExpressionSyntax expr)
            invocations = expr.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>();
        else
            invocations = Enumerable.Empty<InvocationExpressionSyntax>();

        foreach (var inv in invocations)
        {
            if (inv.Expression is not MemberAccessExpressionSyntax ma)
                continue;

            var methodName = ma.Name.Identifier.ValueText;

            // tiny.UseGlobalMiddleware(typeof(Mw<>/Mw<,>))
            if (string.Equals(methodName, "UseGlobalMiddleware", StringComparison.Ordinal))
            {
                var mwOpen = TryExtractOpenGenericTypeFromSingleTypeofArgument(inv, model);
                if (mwOpen is null) continue;

                if (!_mwFactory.TryCreate(compilation, mwOpen, expectedContextFqn, out var mwRef, out var diag))
                {
                    if (diag != null) diags.Add(diag);
                    continue;
                }

                globals.Add(new OrderedEntry(mwRef, OrderKey.From(inv)));
                continue;
            }

            // tiny.UseMiddlewareFor<TCommand>(typeof(Mw<>/Mw<,>)) OR tiny.UseMiddlewareFor(typeof(cmd), typeof(mw))
            if (string.Equals(methodName, "UseMiddlewareFor", StringComparison.Ordinal))
            {
                var genericName = ma.Name as GenericNameSyntax;
                if (genericName != null && genericName.TypeArgumentList.Arguments.Count == 1)
                {
                    var cmdTypeSyntax = genericName.TypeArgumentList.Arguments[0];
                    var cmdType = model.GetTypeInfo(cmdTypeSyntax).Type;
                    if (cmdType is null) continue;

                    var mwOpen = TryExtractOpenGenericTypeFromSingleTypeofArgument(inv, model);
                    if (mwOpen is null) continue;

                    if (!_mwFactory.TryCreate(compilation, mwOpen, expectedContextFqn, out var mwRef, out var diag))
                    {
                        if (diag != null) diags.Add(diag);
                        continue;
                    }

                    var cmdFqn = Fqn.EnsureGlobal(cmdType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                    perCmd.Add(new OrderedPerCommandEntry(cmdFqn, mwRef, OrderKey.From(inv)));
                    continue;
                }

                if (!TryExtractCommandAndMiddlewareFromTwoTypeofArguments(inv, model, out var cmdType2, out var mwOpen2))
                    continue;

                if (!_mwFactory.TryCreate(compilation, mwOpen2, expectedContextFqn, out var mwRef2, out var diag2))
                {
                    if (diag2 != null) diags.Add(diag2);
                    continue;
                }

                var cmdFqn2 = Fqn.EnsureGlobal(cmdType2.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                perCmd.Add(new OrderedPerCommandEntry(cmdFqn2, mwRef2, OrderKey.From(inv)));
                continue;
            }

            // tiny.UsePolicy<TPolicy>() OR services.UseTinyPolicy<TPolicy>() OR services.UseTinyPolicy(typeof(TPolicy))
            if (string.Equals(methodName, "UsePolicy", StringComparison.Ordinal) ||
                string.Equals(methodName, "UseTinyPolicy", StringComparison.Ordinal))
            {
                if (ma.Name is GenericNameSyntax g && g.TypeArgumentList.Arguments.Count == 1)
                {
                    var policyTypeSyntax = g.TypeArgumentList.Arguments[0];
                    var policyType = model.GetTypeInfo(policyTypeSyntax).Type as INamedTypeSymbol;
                    if (policyType is null) continue;

                    policies.Add(policyType);
                    continue;
                }

                if (inv.ArgumentList != null && inv.ArgumentList.Arguments.Count == 1)
                {
                    var toe = inv.ArgumentList.Arguments[0].Expression as TypeOfExpressionSyntax;
                    if (toe is null) continue;

                    var policyType = model.GetSymbolInfo(toe.Type).Symbol as INamedTypeSymbol;
                    if (policyType is null) continue;

                    policies.Add(policyType);
                    continue;
                }
            }
        }
    }

    private static INamedTypeSymbol? TryExtractOpenGenericTypeFromSingleTypeofArgument(
        InvocationExpressionSyntax inv,
        SemanticModel model)
    {
        if (inv.ArgumentList is null || inv.ArgumentList.Arguments.Count != 1)
            return null;

        var toe = inv.ArgumentList.Arguments[0].Expression as TypeOfExpressionSyntax;
        if (toe is null)
            return null;

        var sym = model.GetSymbolInfo(toe.Type).Symbol as INamedTypeSymbol;
        return sym?.OriginalDefinition;
    }

    private static bool TryExtractCommandAndMiddlewareFromTwoTypeofArguments(
        InvocationExpressionSyntax inv,
        SemanticModel model,
        out ITypeSymbol commandType,
        out INamedTypeSymbol middlewareOpenType)
    {
        commandType = null!;
        middlewareOpenType = null!;

        if (inv.ArgumentList is null || inv.ArgumentList.Arguments.Count < 2)
            return false;

        var a0 = inv.ArgumentList.Arguments[0].Expression as TypeOfExpressionSyntax;
        var a1 = inv.ArgumentList.Arguments[1].Expression as TypeOfExpressionSyntax;

        if (a0 is null || a1 is null)
            return false;

        var cmdSym = model.GetSymbolInfo(a0.Type).Symbol as ITypeSymbol;
        var mwSym = model.GetSymbolInfo(a1.Type).Symbol as INamedTypeSymbol;

        if (cmdSym is null || mwSym is null)
            return false;

        commandType = cmdSym;
        middlewareOpenType = mwSym.OriginalDefinition;
        return true;
    }
}
