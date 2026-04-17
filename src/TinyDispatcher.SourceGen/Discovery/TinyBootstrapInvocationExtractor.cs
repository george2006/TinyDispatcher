#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Discovery;

internal sealed class TinyBootstrapInvocationExtractor
{
    private const string UseGlobalMiddlewareMethodName = "UseGlobalMiddleware";
    private const string UseMiddlewareForMethodName = "UseMiddlewareFor";
    private const string UsePolicyMethodName = "UsePolicy";
    private const string UseTinyPolicyMethodName = "UseTinyPolicy";

    public void Extract(
        InvocationExpressionSyntax useTinyCall,
        Compilation compilation,
        List<OrderedEntry> globals,
        List<OrderedPerCommandEntry> perCmd,
        List<INamedTypeSymbol> policies)
    {
        if (useTinyCall.ArgumentList is null)
        {
            return;
        }

        var model = compilation.GetSemanticModel(useTinyCall.SyntaxTree);

        var lambda = SelectBootstrapLambda(useTinyCall, model);
        if (lambda is null)
        {
            return;
        }

        var invocations = GetBootstrapInvocations(lambda);

        for (var i = 0; i < invocations.Count; i++)
        {
            ExtractBootstrapInvocation(
                invocations[i],
                model,
                globals,
                perCmd,
                policies);
        }
    }

    private static List<InvocationExpressionSyntax> GetBootstrapInvocations(LambdaExpressionSyntax lambda)
    {
        var invocations = new List<InvocationExpressionSyntax>();

        if (lambda.Body is BlockSyntax block)
        {
            AddDescendantInvocations(invocations, block);
            return invocations;
        }

        if (lambda.Body is ExpressionSyntax expression)
        {
            AddSelfAndDescendantInvocations(invocations, expression);
        }

        return invocations;
    }

    private static void AddDescendantInvocations(
        List<InvocationExpressionSyntax> invocations,
        BlockSyntax block)
    {
        foreach (var node in block.DescendantNodes())
        {
            if (node is InvocationExpressionSyntax invocation)
            {
                invocations.Add(invocation);
            }
        }
    }

    private static void AddSelfAndDescendantInvocations(
        List<InvocationExpressionSyntax> invocations,
        ExpressionSyntax expression)
    {
        foreach (var node in expression.DescendantNodesAndSelf())
        {
            if (node is InvocationExpressionSyntax invocation)
            {
                invocations.Add(invocation);
            }
        }
    }

    private static void ExtractBootstrapInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        List<OrderedEntry> globals,
        List<OrderedPerCommandEntry> perCommand,
        List<INamedTypeSymbol> policies)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;
        var isGlobalMiddlewareCall = string.Equals(methodName, UseGlobalMiddlewareMethodName, StringComparison.Ordinal);
        if (isGlobalMiddlewareCall)
        {
            AddGlobalMiddlewareIfPresent(invocation, model, globals);
            return;
        }

        var isPerCommandMiddlewareCall = string.Equals(methodName, UseMiddlewareForMethodName, StringComparison.Ordinal);
        if (isPerCommandMiddlewareCall)
        {
            AddPerCommandMiddlewareIfPresent(invocation, memberAccess, model, perCommand);
            return;
        }

        var isPolicyCall =
            string.Equals(methodName, UsePolicyMethodName, StringComparison.Ordinal) ||
            string.Equals(methodName, UseTinyPolicyMethodName, StringComparison.Ordinal);

        if (isPolicyCall)
        {
            AddPolicyIfPresent(invocation, memberAccess, model, policies);
        }
    }

    private static void AddGlobalMiddlewareIfPresent(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        List<OrderedEntry> globals)
    {
        var middlewareOpenType = TryExtractOpenGenericType(invocation, model);
        if (middlewareOpenType is null)
        {
            return;
        }

        globals.Add(new OrderedEntry(
            CreateMiddlewareRef(middlewareOpenType),
            OrderKey.From(invocation)));
    }

    private static void AddPerCommandMiddlewareIfPresent(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess,
        SemanticModel model,
        List<OrderedPerCommandEntry> perCommand)
    {
        if (TryAddGenericPerCommandMiddleware(invocation, memberAccess, model, perCommand))
        {
            return;
        }

        AddTypeofPerCommandMiddlewareIfPresent(invocation, model, perCommand);
    }

    private static bool TryAddGenericPerCommandMiddleware(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess,
        SemanticModel model,
        List<OrderedPerCommandEntry> perCommand)
    {
        if (memberAccess.Name is not GenericNameSyntax genericName)
        {
            return false;
        }

        var hasSingleGenericArgument = genericName.TypeArgumentList.Arguments.Count == 1;
        if (!hasSingleGenericArgument)
        {
            return false;
        }

        var commandType = model.GetTypeInfo(genericName.TypeArgumentList.Arguments[0]).Type;
        var middlewareOpenType = TryExtractOpenGenericType(invocation, model);

        if (commandType is null || middlewareOpenType is null)
        {
            return true;
        }

        AddPerCommandMiddleware(invocation, commandType, middlewareOpenType, perCommand);
        return true;
    }

    private static void AddTypeofPerCommandMiddlewareIfPresent(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        List<OrderedPerCommandEntry> perCommand)
    {
        if (!TryExtractCommandAndMiddleware(invocation, model, out var commandType, out var middlewareOpenType))
        {
            return;
        }

        AddPerCommandMiddleware(invocation, commandType, middlewareOpenType, perCommand);
    }

    private static void AddPerCommandMiddleware(
        InvocationExpressionSyntax invocation,
        ITypeSymbol commandType,
        INamedTypeSymbol middlewareOpenType,
        List<OrderedPerCommandEntry> perCommand)
    {
        var commandFqn = Fqn.EnsureGlobal(
            commandType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

        perCommand.Add(new OrderedPerCommandEntry(
            commandFqn,
            CreateMiddlewareRef(middlewareOpenType),
            OrderKey.From(invocation)));
    }

    private static void AddPolicyIfPresent(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess,
        SemanticModel model,
        List<INamedTypeSymbol> policies)
    {
        if (TryAddGenericPolicy(memberAccess, model, policies))
        {
            return;
        }

        AddTypeofPolicyIfPresent(invocation, model, policies);
    }

    private static bool TryAddGenericPolicy(
        MemberAccessExpressionSyntax memberAccess,
        SemanticModel model,
        List<INamedTypeSymbol> policies)
    {
        if (memberAccess.Name is not GenericNameSyntax genericName)
        {
            return false;
        }

        var hasSingleGenericArgument = genericName.TypeArgumentList.Arguments.Count == 1;
        if (!hasSingleGenericArgument)
        {
            return false;
        }

        var policyType = model.GetTypeInfo(genericName.TypeArgumentList.Arguments[0]).Type as INamedTypeSymbol;
        if (policyType is not null)
        {
            policies.Add(policyType);
        }

        return true;
    }

    private static void AddTypeofPolicyIfPresent(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        List<INamedTypeSymbol> policies)
    {
        var hasSingleArgument = invocation.ArgumentList?.Arguments.Count == 1;
        if (!hasSingleArgument)
        {
            return;
        }

        if (invocation.ArgumentList!.Arguments[0].Expression is not TypeOfExpressionSyntax typeOfExpression)
        {
            return;
        }

        var policyType = model.GetSymbolInfo(typeOfExpression.Type).Symbol as INamedTypeSymbol;
        if (policyType is not null)
        {
            policies.Add(policyType);
        }
    }

    private static LambdaExpressionSyntax? SelectBootstrapLambda(
        InvocationExpressionSyntax useTinyCall,
        SemanticModel model)
    {
        if (useTinyCall.ArgumentList is null)
        {
            return null;
        }

        foreach (var arg in useTinyCall.ArgumentList.Arguments)
        {
            if (arg.Expression is not LambdaExpressionSyntax lambda)
            {
                continue;
            }

            if (IsTinyBootstrapDelegate(lambda, model))
            {
                return lambda;
            }
        }

        return SelectFirstLambda(useTinyCall);
    }

    private static LambdaExpressionSyntax? SelectFirstLambda(InvocationExpressionSyntax useTinyCall)
    {
        if (useTinyCall.ArgumentList is null)
        {
            return null;
        }

        foreach (var argument in useTinyCall.ArgumentList.Arguments)
        {
            if (argument.Expression is LambdaExpressionSyntax lambda)
            {
                return lambda;
            }
        }

        return null;
    }

    private static bool IsTinyBootstrapDelegate(LambdaExpressionSyntax lambda, SemanticModel model)
    {
        var converted = model.GetTypeInfo(lambda).ConvertedType as INamedTypeSymbol;
        var invoke = converted?.DelegateInvokeMethod;
        if (invoke is null)
        {
            return false;
        }

        if (invoke.Parameters.Length != 1)
        {
            return false;
        }

        var parameterType = invoke.Parameters[0].Type;

        if (parameterType.Name == "TinyBootstrap")
        {
            return true;
        }

        var fqn = parameterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
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
        var hasGenericSuffix = idx >= 0;

        if (!hasGenericSuffix)
        {
            return fqn;
        }

        return fqn.Substring(0, idx);
    }

    private static INamedTypeSymbol? TryExtractOpenGenericType(
        InvocationExpressionSyntax inv,
        SemanticModel model)
    {
        if (inv.ArgumentList?.Arguments.Count != 1)
        {
            return null;
        }

        if (inv.ArgumentList.Arguments[0].Expression is not TypeOfExpressionSyntax toe)
        {
            return null;
        }

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

        var argumentList = inv.ArgumentList;
        if (argumentList is null)
        {
            return false;
        }

        var hasTooFewArguments = argumentList.Arguments.Count < 2;
        if (hasTooFewArguments)
        {
            return false;
        }

        var commandTypeOf = argumentList.Arguments[0].Expression as TypeOfExpressionSyntax;
        var middlewareTypeOf = argumentList.Arguments[1].Expression as TypeOfExpressionSyntax;

        if (commandTypeOf is null || middlewareTypeOf is null)
        {
            return false;
        }

        var commandSymbol = model.GetSymbolInfo(commandTypeOf.Type).Symbol as ITypeSymbol;
        var middlewareSymbol = model.GetSymbolInfo(middlewareTypeOf.Type).Symbol as INamedTypeSymbol;

        if (commandSymbol is null || middlewareSymbol is null)
        {
            return false;
        }

        commandType = commandSymbol;
        middlewareOpenType = middlewareSymbol.OriginalDefinition;
        return true;
    }
}
