#nullable enable

using Microsoft.CodeAnalysis;
using System;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Validation;

internal sealed class MiddlewareRefShapeValidator : IGeneratorValidator
{
    public void Validate(
        GeneratorValidationContext context,
        INamedTypeSymbol? commandMiddlewareInterface,
        MiddlewareTypeResolver middlewareTypeResolver,
        DiagnosticBag diags)
    {
        GuardInputs(context, middlewareTypeResolver, diags);

        var contextTypeFqn = context.ContextTypeFqn;
        if (ShouldSkipMiddlewareValidation(context, contextTypeFqn))
        {
            return;
        }

        if (commandMiddlewareInterface is null)
        {
            ReportCannotResolveMiddlewareContract(context, diags);
            return;
        }

        foreach (var middleware in context.EnumerateAllMiddlewares())
        {
            ValidateMiddlewareRef(
                context,
                diags,
                middlewareTypeResolver,
                commandMiddlewareInterface,
                contextTypeFqn,
                middleware);
        }
    }

    private static void GuardInputs(
        GeneratorValidationContext context,
        MiddlewareTypeResolver middlewareTypeResolver,
        DiagnosticBag diags)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (middlewareTypeResolver is null)
        {
            throw new ArgumentNullException(nameof(middlewareTypeResolver));
        }

        if (diags is null)
        {
            throw new ArgumentNullException(nameof(diags));
        }
    }

    private static bool ShouldSkipMiddlewareValidation(
        GeneratorValidationContext context,
        string? contextTypeFqn)
    {
        var pipelinesAreNotEmitted = !context.IsHostProject;
        if (pipelinesAreNotEmitted)
        {
            return true;
        }

        var contextTypeIsMissing = string.IsNullOrWhiteSpace(contextTypeFqn);
        if (contextTypeIsMissing)
        {
            return true; // ContextConsistencyValidator will report the real issue.
        }

        return false;
    }

    private static void ValidateMiddlewareRef(
        GeneratorValidationContext context,
        DiagnosticBag diags,
        MiddlewareTypeResolver middlewareTypeResolver,
        INamedTypeSymbol commandMiddlewareInterface,
        string contextTypeFqn,
        MiddlewareRef middleware)
    {
        var openType = middlewareTypeResolver.Resolve(middleware.OpenTypeFqn, middleware.Arity);
        if (openType is null)
        {
            ReportInvalidMiddlewareType(context, diags, middleware);
            return;
        }

        if (!HasExpectedOpenGenericShape(openType, middleware))
        {
            ReportInvalidMiddlewareType(context, diags, middleware);
            return;
        }

        if (!HasSupportedMiddlewareArity(middleware))
        {
            ReportUnsupportedMiddlewareArity(context, diags, middleware);
            return;
        }

        if (UsesOpenContextParameter(middleware))
        {
            ValidateOpenGenericMiddleware(
                context,
                diags,
                commandMiddlewareInterface,
                openType,
                middleware);

            return;
        }

        ValidateContextClosedMiddleware(
            context,
            diags,
            commandMiddlewareInterface,
            openType,
            contextTypeFqn,
            middleware);
    }

    private static bool HasExpectedOpenGenericShape(
        INamedTypeSymbol openType,
        MiddlewareRef middleware)
    {
        return openType.IsGenericType &&
               openType.TypeParameters.Length == middleware.Arity;
    }

    private static bool HasSupportedMiddlewareArity(MiddlewareRef middleware)
    {
        return middleware.Arity == 1 ||
               middleware.Arity == 2;
    }

    private static bool UsesOpenContextParameter(MiddlewareRef middleware)
    {
        return middleware.Arity == 2;
    }

    private static void ValidateOpenGenericMiddleware(
        GeneratorValidationContext context,
        DiagnosticBag diags,
        INamedTypeSymbol commandMiddlewareInterface,
        INamedTypeSymbol openType,
        MiddlewareRef middleware)
    {
        if (ImplementsOpenGenericMiddlewareContract(openType, commandMiddlewareInterface))
        {
            return;
        }

        ReportInvalidMiddlewareType(context, diags, middleware);
    }

    private static bool ImplementsOpenGenericMiddlewareContract(
        INamedTypeSymbol openType,
        INamedTypeSymbol commandMiddlewareInterface)
    {
        foreach (var iface in openType.AllInterfaces)
        {
            if (IsOpenGenericMiddlewareContract(iface, commandMiddlewareInterface))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOpenGenericMiddlewareContract(
        INamedTypeSymbol iface,
        INamedTypeSymbol commandMiddlewareInterface)
    {
        if (!IsCommandMiddlewareInterface(iface, commandMiddlewareInterface))
        {
            return false;
        }

        var hasExpectedTypeArgumentCount = iface.TypeArguments.Length == 2;
        if (!hasExpectedTypeArgumentCount)
        {
            return false;
        }

        if (!IsTypeParameterAtOrdinal(iface.TypeArguments[0], ordinal: 0))
        {
            return false;
        }

        return IsTypeParameterAtOrdinal(
            iface.TypeArguments[1],
            ordinal: 1);
    }

    private static void ValidateContextClosedMiddleware(
        GeneratorValidationContext context,
        DiagnosticBag diags,
        INamedTypeSymbol commandMiddlewareInterface,
        INamedTypeSymbol openType,
        string contextTypeFqn,
        MiddlewareRef middleware)
    {
        var matchingContractCount = CountContextClosedMiddlewareContracts(
            openType,
            commandMiddlewareInterface,
            contextTypeFqn);

        var hasExactlyOneMatchingContract = matchingContractCount == 1;
        if (hasExactlyOneMatchingContract)
        {
            return;
        }

        ReportInvalidContextClosedMiddleware(context, diags, contextTypeFqn, middleware);
    }

    private static int CountContextClosedMiddlewareContracts(
        INamedTypeSymbol openType,
        INamedTypeSymbol commandMiddlewareInterface,
        string contextTypeFqn)
    {
        var matches = 0;

        foreach (var iface in openType.AllInterfaces)
        {
            if (!IsMatchingContextClosedMiddlewareContract(
                    iface,
                    commandMiddlewareInterface,
                    contextTypeFqn))
            {
                continue;
            }

            matches++;
        }

        return matches;
    }

    private static bool IsMatchingContextClosedMiddlewareContract(
        INamedTypeSymbol iface,
        INamedTypeSymbol commandMiddlewareInterface,
        string contextTypeFqn)
    {
        if (!IsCommandMiddlewareInterface(iface, commandMiddlewareInterface))
        {
            return false;
        }

        var hasExpectedTypeArgumentCount = iface.TypeArguments.Length == 2;
        if (!hasExpectedTypeArgumentCount)
        {
            return false;
        }

        if (!IsTypeParameterAtOrdinal(iface.TypeArguments[0], ordinal: 0))
        {
            return false;
        }

        var contextArgumentIsClosedType = iface.TypeArguments[1] is not ITypeParameterSymbol;
        if (!contextArgumentIsClosedType)
        {
            return false;
        }

        var middlewareContextFqn = Fqn.EnsureGlobal(
            iface.TypeArguments[1]
                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

        var middlewareContextMatchesCurrentContext = string.Equals(
            middlewareContextFqn,
            contextTypeFqn,
            StringComparison.Ordinal);

        return middlewareContextMatchesCurrentContext;
    }

    private static bool IsCommandMiddlewareInterface(
        INamedTypeSymbol iface,
        INamedTypeSymbol commandMiddlewareInterface)
    {
        return SymbolEqualityComparer.Default.Equals(
            iface.OriginalDefinition,
            commandMiddlewareInterface);
    }

    private static bool IsTypeParameterAtOrdinal(ITypeSymbol type, int ordinal)
    {
        return type is ITypeParameterSymbol typeParameter &&
               typeParameter.Ordinal == ordinal;
    }

    private static void ReportCannotResolveMiddlewareContract(
        GeneratorValidationContext context,
        DiagnosticBag diags)
    {
        diags.Add(context.Diagnostics.Create(
            context.Diagnostics.CannotResolveICommandMiddleware,
            Location.None));
    }

    private static void ReportInvalidMiddlewareType(
        GeneratorValidationContext context,
        DiagnosticBag diags,
        MiddlewareRef middleware)
    {
        diags.Add(context.Diagnostics.Create(
            context.Diagnostics.InvalidMiddlewareType,
            Location.None,
            middleware.OpenTypeFqn));
    }

    private static void ReportUnsupportedMiddlewareArity(
        GeneratorValidationContext context,
        DiagnosticBag diags,
        MiddlewareRef middleware)
    {
        diags.Add(context.Diagnostics.Create(
            context.Diagnostics.UnsupportedMiddlewareArity,
            Location.None,
            middleware.OpenTypeFqn));
    }

    private static void ReportInvalidContextClosedMiddleware(
        GeneratorValidationContext context,
        DiagnosticBag diags,
        string contextTypeFqn,
        MiddlewareRef middleware)
    {
        diags.Add(context.Diagnostics.Create(
            context.Diagnostics.InvalidContextClosedMiddleware,
            Location.None,
            middleware.OpenTypeFqn,
            contextTypeFqn));
    }
}
