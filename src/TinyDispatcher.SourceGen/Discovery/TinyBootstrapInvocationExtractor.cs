#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Discovery;

internal sealed class TinyBootstrapInvocationExtractor
{
    public void Extract(
        InvocationExpressionSyntax useTinyCall,
        Compilation compilation,
        List<OrderedEntry> globals,
        List<OrderedPerCommandEntry> perCmd,
        List<INamedTypeSymbol> policies)
    {
        if (useTinyCall.ArgumentList is null)
            return;

        var model = compilation.GetSemanticModel(useTinyCall.SyntaxTree);

        var lambda = SelectBootstrapLambda(useTinyCall, model);
        if (lambda is null)
            return;

        IEnumerable<InvocationExpressionSyntax> invocations =
            lambda.Body switch
            {
                BlockSyntax block => block.DescendantNodes().OfType<InvocationExpressionSyntax>(),
                ExpressionSyntax expr => expr.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>(),
                _ => Enumerable.Empty<InvocationExpressionSyntax>()
            };

        foreach (var inv in invocations)
        {
            if (inv.Expression is not MemberAccessExpressionSyntax ma)
                continue;

            var methodName = ma.Name.Identifier.ValueText;

            if (string.Equals(methodName, "UseGlobalMiddleware", StringComparison.Ordinal))
            {
                var mwOpen = TryExtractOpenGenericType(inv, model);
                if (mwOpen is null)
                    continue;

                globals.Add(new OrderedEntry(CreateMiddlewareRef(mwOpen), OrderKey.From(inv)));
                continue;
            }

            if (string.Equals(methodName, "UseMiddlewareFor", StringComparison.Ordinal))
            {
                var genericName = ma.Name as GenericNameSyntax;

                if (genericName != null && genericName.TypeArgumentList.Arguments.Count == 1)
                {
                    var cmdType = model.GetTypeInfo(genericName.TypeArgumentList.Arguments[0]).Type;
                    var mwOpen = TryExtractOpenGenericType(inv, model);
                    if (cmdType is null || mwOpen is null)
                        continue;

                    var cmdFqn = Fqn.EnsureGlobal(
                        cmdType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

                    perCmd.Add(new OrderedPerCommandEntry(
                        cmdFqn,
                        CreateMiddlewareRef(mwOpen),
                        OrderKey.From(inv)));

                    continue;
                }

                if (TryExtractCommandAndMiddleware(inv, model, out var cmdType2, out var mwOpen2))
                {
                    var cmdFqn2 = Fqn.EnsureGlobal(
                        cmdType2.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

                    perCmd.Add(new OrderedPerCommandEntry(
                        cmdFqn2,
                        CreateMiddlewareRef(mwOpen2),
                        OrderKey.From(inv)));
                }

                continue;
            }

            if (string.Equals(methodName, "UsePolicy", StringComparison.Ordinal) ||
                string.Equals(methodName, "UseTinyPolicy", StringComparison.Ordinal))
            {
                if (ma.Name is GenericNameSyntax g && g.TypeArgumentList.Arguments.Count == 1)
                {
                    var policyType = model.GetTypeInfo(g.TypeArgumentList.Arguments[0]).Type as INamedTypeSymbol;
                    if (policyType is not null)
                        policies.Add(policyType);

                    continue;
                }

                if (inv.ArgumentList?.Arguments.Count == 1 &&
                    inv.ArgumentList.Arguments[0].Expression is TypeOfExpressionSyntax toe)
                {
                    var policyType = model.GetSymbolInfo(toe.Type).Symbol as INamedTypeSymbol;
                    if (policyType is not null)
                        policies.Add(policyType);
                }
            }
        }
    }

    private static LambdaExpressionSyntax? SelectBootstrapLambda(
        InvocationExpressionSyntax useTinyCall,
        SemanticModel model)
    {
        foreach (var arg in useTinyCall.ArgumentList!.Arguments)
        {
            if (arg.Expression is not LambdaExpressionSyntax lambda)
                continue;

            if (IsTinyBootstrapDelegate(lambda, model))
                return lambda;
        }

        // Fallback: old behavior (first lambda)
        return useTinyCall.ArgumentList.Arguments
            .Select(a => a.Expression)
            .OfType<LambdaExpressionSyntax>()
            .FirstOrDefault();
    }

    private static bool IsTinyBootstrapDelegate(LambdaExpressionSyntax lambda, SemanticModel model)
    {
        var converted = model.GetTypeInfo(lambda).ConvertedType as INamedTypeSymbol;
        var invoke = converted?.DelegateInvokeMethod;
        if (invoke is null)
            return false;

        if (invoke.Parameters.Length != 1)
            return false;

        var p0 = invoke.Parameters[0].Type;

        // Fast path:
        if (p0.Name == "TinyBootstrap")
            return true;

        // Safer path (avoids false positives if multiple TinyBootstrap types exist):
        var fqn = p0.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return string.Equals(fqn, "global::TinyDispatcher.TinyBootstrap", StringComparison.Ordinal);
    }

    private static MiddlewareRef CreateMiddlewareRef(INamedTypeSymbol middlewareOpenType)
    {
        var open = middlewareOpenType.OriginalDefinition;

        var fqnWithArgs = Fqn.EnsureGlobal(
            open.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

        var baseFqn = StripGenericSuffix(fqnWithArgs);

        return new MiddlewareRef(open, baseFqn, open.Arity);
    }

    private static string StripGenericSuffix(string fqn)
    {
        var idx = fqn.IndexOf('<');
        return idx < 0 ? fqn : fqn.Substring(0, idx);
    }

    private static INamedTypeSymbol? TryExtractOpenGenericType(
        InvocationExpressionSyntax inv,
        SemanticModel model)
    {
        if (inv.ArgumentList?.Arguments.Count != 1)
            return null;

        if (inv.ArgumentList.Arguments[0].Expression is not TypeOfExpressionSyntax toe)
            return null;

        var sym = model.GetSymbolInfo(toe.Type).Symbol as INamedTypeSymbol;
        return sym?.OriginalDefinition;
    }

    private static bool TryExtractCommandAndMiddleware(
        InvocationExpressionSyntax inv,
        SemanticModel model,
        out ITypeSymbol commandType,
        out INamedTypeSymbol middlewareOpenType)
    {
        commandType = null!;
        middlewareOpenType = null!;

        if (inv.ArgumentList?.Arguments.Count < 2)
            return false;

        var a0 = inv.ArgumentList!.Arguments[0].Expression as TypeOfExpressionSyntax;
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