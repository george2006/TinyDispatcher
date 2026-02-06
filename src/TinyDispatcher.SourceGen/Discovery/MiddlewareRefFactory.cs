using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;
using TinyDispatcher.SourceGen.Generator;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Discovery;

internal sealed class MiddlewareRefFactory
{
    private readonly DiagnosticsCatalog _diags;

    public MiddlewareRefFactory(DiagnosticsCatalog diags)
        => _diags = diags ?? throw new ArgumentNullException(nameof(diags));

    public bool TryCreate(
        Compilation compilation,
        INamedTypeSymbol openMiddlewareType,
        string expectedContextFqn,
        out MiddlewareRef middleware,
        out Diagnostic? diagnostic)
    {
        middleware = default;
        diagnostic = null;

        if (!openMiddlewareType.IsGenericType || !openMiddlewareType.IsDefinition)
        {
            diagnostic = _diags.CreateError(
                "DISP301",
                "Invalid middleware type",
                $"Middleware '{openMiddlewareType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}' must be an open generic type definition (e.g. typeof(MyMiddleware<,>) or typeof(MyMiddleware<>)).");
            return false;
        }

        var arity = openMiddlewareType.Arity;

        if (arity != 1 && arity != 2)
        {
            diagnostic = _diags.CreateError(
                "DISP302",
                "Unsupported middleware arity",
                $"Middleware '{openMiddlewareType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}' must have arity 1 or 2.");
            return false;
        }

        var fmtNoGenerics = SymbolDisplayFormat.FullyQualifiedFormat
            .WithGenericsOptions(SymbolDisplayGenericsOptions.None);

        var openFqn = Fqn.EnsureGlobal(openMiddlewareType.ToDisplayString(fmtNoGenerics));

        if (arity == 2)
        {
            middleware = new MiddlewareRef(openFqn, 2);
            return true;
        }

        // Arity 1: must implement exactly one ICommandMiddleware<TCommand, TContextClosed>
        var iface = compilation.GetTypeByMetadataName("TinyDispatcher.ICommandMiddleware`2");
        if (iface is null)
        {
            diagnostic = _diags.CreateError(
                "DISP303",
                "Cannot resolve ICommandMiddleware",
                "Could not resolve 'TinyDispatcher.ICommandMiddleware`2' from compilation.");
            return false;
        }

        var matches = 0;

        foreach (var i in openMiddlewareType.AllInterfaces)
        {
            if (!SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iface))
                continue;

            if (i.TypeArguments.Length != 2)
                continue;

            // TCommand must be the middleware generic parameter #0
            if (i.TypeArguments[0] is not ITypeParameterSymbol tp || tp.Ordinal != 0)
                continue;

            // TContext must be CLOSED
            if (i.TypeArguments[1] is ITypeParameterSymbol)
                continue;

            var ctxFqn = Fqn.EnsureGlobal(i.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            if (!string.Equals(ctxFqn, expectedContextFqn, StringComparison.Ordinal))
                continue;

            matches++;
        }

        if (matches != 1)
        {
            diagnostic = _diags.CreateError(
                "DISP304",
                "Invalid context-closed middleware",
                $"Middleware '{openMiddlewareType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}' must implement exactly one ICommandMiddleware<TCommand, {expectedContextFqn}>.");
            return false;
        }

        middleware = new MiddlewareRef(openFqn, 1);
        return true;
    }
}
