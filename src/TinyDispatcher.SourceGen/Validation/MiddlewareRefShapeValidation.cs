#nullable enable

using Microsoft.CodeAnalysis;
using System;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Validation;

internal sealed class MiddlewareRefShapeValidator : IGeneratorValidator
{
    public void Validate(GeneratorValidationContext context, DiagnosticBag diags)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (diags is null) throw new ArgumentNullException(nameof(diags));

        // If pipelines are not emitted, skip middleware validation.
        if (!context.IsHostProject)
            return;

        var expectedContextFqn = context.ExpectedContextFqn;
        if (string.IsNullOrWhiteSpace(expectedContextFqn))
            return; // ContextConsistencyValidator will report the real issue.

        var compilation = context.Compilation;

        var iCmdMw2 = compilation.GetTypeByMetadataName("TinyDispatcher.ICommandMiddleware`2");
        if (iCmdMw2 is null)
        {
            diags.Add(context.Diagnostics.Create(
                context.Diagnostics.CannotResolveICommandMiddleware,
                Location.None));
            return;
        }

        foreach (var mw in context.EnumerateAllMiddlewares())
        {
            var openType = mw.OpenTypeSymbol;

            // DISP301: must be open generic definition
            if (!openType.IsGenericType || openType.TypeParameters.Length != mw.Arity)
            {
                diags.Add(context.Diagnostics.Create(
                    context.Diagnostics.InvalidMiddlewareType,
                    Location.None,
                    mw.OpenTypeFqn));
                continue;
            }

            // DISP302: only arity 1 or 2 supported
            if (mw.Arity != 1 && mw.Arity != 2)
            {
                diags.Add(context.Diagnostics.Create(
                    context.Diagnostics.UnsupportedMiddlewareArity,
                    Location.None,
                    mw.OpenTypeFqn));
                continue;
            }

            // -----------------------------------------------------------------
            // Arity 2: must implement ICommandMiddleware<TCommand, TContext>
            // where TCommand = type param 0
            //       TContext = type param 1
            // -----------------------------------------------------------------
            if (mw.Arity == 2)
            {
                var ok = false;

                foreach (var iface in openType.AllInterfaces)
                {
                    if (!SymbolEqualityComparer.Default.Equals(
                            iface.OriginalDefinition,
                            iCmdMw2))
                        continue;

                    if (iface.TypeArguments.Length != 2)
                        continue;

                    if (iface.TypeArguments[0] is not ITypeParameterSymbol tp0 || tp0.Ordinal != 0)
                        continue;

                    if (iface.TypeArguments[1] is not ITypeParameterSymbol tp1 || tp1.Ordinal != 1)
                        continue;

                    ok = true;
                    break;
                }

                if (!ok)
                {
                    diags.Add(context.Diagnostics.Create(
                        context.Diagnostics.InvalidMiddlewareType,
                        Location.None,
                        mw.OpenTypeFqn));
                }

                continue;
            }

            // -----------------------------------------------------------------
            // Arity 1: context-closed middleware
            // Must implement exactly one:
            // ICommandMiddleware<TCommand, ExpectedContext>
            // where TCommand = type param 0
            //       TContext = CLOSED and matches expected context
            // -----------------------------------------------------------------
            var matches = 0;

            foreach (var iface in openType.AllInterfaces)
            {
                if (!SymbolEqualityComparer.Default.Equals(
                        iface.OriginalDefinition,
                        iCmdMw2))
                    continue;

                if (iface.TypeArguments.Length != 2)
                    continue;

                // TCommand must be generic parameter #0
                if (iface.TypeArguments[0] is not ITypeParameterSymbol tp || tp.Ordinal != 0)
                    continue;

                // TContext must be CLOSED
                if (iface.TypeArguments[1] is ITypeParameterSymbol)
                    continue;

                var ctxFqn = Fqn.EnsureGlobal(
                    iface.TypeArguments[1]
                        .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

                if (!string.Equals(ctxFqn, expectedContextFqn, StringComparison.Ordinal))
                    continue;

                matches++;
            }

            if (matches != 1)
            {
                // DISP304 (expected by your test)
                diags.Add(context.Diagnostics.Create(
                    context.Diagnostics.InvalidContextClosedMiddleware,
                    Location.None,
                    mw.OpenTypeFqn,
                    expectedContextFqn));
            }
        }
    }
}
