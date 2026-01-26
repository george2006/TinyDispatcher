using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public sealed record MiddlewareDiscoveryResult(
    ImmutableArray<string> GlobalMiddlewares,
    ImmutableDictionary<string, ImmutableArray<string>> PerCommandMiddlewares);

public sealed class MiddlewareRegistrationDiscovery
{
    private readonly string _coreNs;

    public MiddlewareRegistrationDiscovery(string coreNamespace)
    {
        _coreNs = coreNamespace ?? throw new ArgumentNullException(nameof(coreNamespace));
    }

    public MiddlewareDiscoveryResult Discover(
        Compilation compilation,
        ImmutableArray<InvocationExpressionSyntax> invocations)
    {
        var globals = ImmutableArray.CreateBuilder<string>();
        var perCommand = new Dictionary<string, ImmutableArray<string>.Builder>(StringComparer.Ordinal);

        var modelCache = new Dictionary<SyntaxTree, SemanticModel>();

        foreach (var invoke in invocations)
        {
            var model = GetModel(compilation, modelCache, invoke.SyntaxTree);

            var symbol = model.GetSymbolInfo(invoke).Symbol as IMethodSymbol;
            if (symbol is null)
                continue;

            if (!IsOurRegistrationMethod(symbol))
                continue;

            // --------
            // GLOBAL
            // --------
            if (symbol.Name == "UseDispatcherCommandMiddleware")
            {
                // A) Generic overload: UseDispatcherCommandMiddleware<TMiddleware>()
                if (symbol.TypeArguments.Length == 1)
                {
                    AddOpenMiddleware(globals, symbol.TypeArguments[0]);
                    continue;
                }

                // B) Type overload: UseDispatcherCommandMiddleware(typeof(Mw<>))
                if (TryGetTypeOfArgument(model, invoke, argIndexFromEnd: 1, out var openMw))
                {
                    AddOpenMiddleware(globals, openMw);
                }

                continue;
            }

            // --------
            // PER COMMAND
            // --------
            if (symbol.Name == "UseDispatcherCommandMiddlewareFor")
            {
                // A) Generic command overload: UseDispatcherCommandMiddlewareFor<TCommand>(typeof(Mw<>))
                if (symbol.TypeArguments.Length == 1)
                {
                    var cmd = symbol.TypeArguments[0];
                    if (TryGetTypeOfArgument(model, invoke, argIndexFromEnd: 1, out var openMw))
                    {
                        AddPerCommand(perCommand, cmd, openMw);
                    }
                    continue;
                }

                // B) Non-generic: UseDispatcherCommandMiddlewareFor(typeof(Command), typeof(Mw<>))
                // Expect 2 args: (commandType, openGenericMiddlewareType)
                if (TryGetTypeOfArgument(model, invoke, argIndexFromEnd: 2, out var cmdType) &&
                    TryGetTypeOfArgument(model, invoke, argIndexFromEnd: 1, out var openMw2))
                {
                    AddPerCommand(perCommand, cmdType, openMw2);
                }

                continue;
            }
        }

        var perCmdFinal = perCommand.ToImmutableDictionary(
            kv => kv.Key,
            kv => kv.Value.ToImmutable(),
            StringComparer.Ordinal);

        return new MiddlewareDiscoveryResult(
            GlobalMiddlewares: globals.ToImmutable(),
            PerCommandMiddlewares: perCmdFinal);
    }

    private static void AddPerCommand(
        Dictionary<string, ImmutableArray<string>.Builder> map,
        ITypeSymbol commandType,
        ITypeSymbol middlewareType)
    {
        var cmdFqn = ToFqn(commandType, includeGenerics: true);
        var mwOpenFqn = ToFqn(middlewareType.OriginalDefinition, includeGenerics: false);

        if (string.IsNullOrWhiteSpace(cmdFqn) || string.IsNullOrWhiteSpace(mwOpenFqn))
            return;

        if (!map.TryGetValue(cmdFqn, out var list))
        {
            list = ImmutableArray.CreateBuilder<string>();
            map[cmdFqn] = list;
        }

        list.Add(mwOpenFqn);
    }

    private static void AddOpenMiddleware(ImmutableArray<string>.Builder globals, ITypeSymbol mwType)
    {
        var open = mwType.OriginalDefinition;
        var fqn = ToFqn(open, includeGenerics: false);
        if (!string.IsNullOrWhiteSpace(fqn))
            globals.Add(fqn);
    }

    private static string ToFqn(ITypeSymbol type, bool includeGenerics)
    {
        var fmt = SymbolDisplayFormat.FullyQualifiedFormat;

        if (!includeGenerics)
        {
            fmt = fmt.WithGenericsOptions(SymbolDisplayGenericsOptions.None);
        }

        var s = type.ToDisplayString(fmt);
        // normalize to avoid global::global::
        return s.StartsWith("global::global::", StringComparison.Ordinal)
            ? "global::" + s.Substring("global::global::".Length)
            : s;
    }

    private bool IsOurRegistrationMethod(IMethodSymbol symbol)
    {
        if (symbol.Name != "UseDispatcherCommandMiddleware" &&
            symbol.Name != "UseDispatcherCommandMiddlewareFor")
            return false;

        var containingType = symbol.ContainingType;
        if (containingType is null || containingType.Name != "DispatcherMiddlewareRegistrationExtensions")
            return false;

        var ns = containingType.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return ns.IndexOf(_coreNs, StringComparison.Ordinal) >= 0;
    }

    private static bool TryGetTypeOfArgument(
        SemanticModel model,
        InvocationExpressionSyntax invoke,
        int argIndexFromEnd,
        out ITypeSymbol type)
    {
        type = default!;

        var args = invoke.ArgumentList?.Arguments;
        if (args is null || args.Value.Count == 0)
            return false;

        var idx = args.Value.Count - argIndexFromEnd;
        if (idx < 0 || idx >= args.Value.Count)
            return false;

        if (args.Value[idx].Expression is not TypeOfExpressionSyntax typeOfExpr)
            return false;

        var ti = model.GetTypeInfo(typeOfExpr.Type);
        if (ti.Type is null)
            return false;

        type = ti.Type;
        return true;
    }

    private static SemanticModel GetModel(
        Compilation compilation,
        Dictionary<SyntaxTree, SemanticModel> cache,
        SyntaxTree tree)
    {
        if (!cache.TryGetValue(tree, out var model))
        {
            model = compilation.GetSemanticModel(tree);
            cache[tree] = model;
        }

        return model;
    }
}
