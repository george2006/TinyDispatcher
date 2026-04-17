using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record GeneratorInput(
    Compilation Compilation,
    ImmutableArray<INamedTypeSymbol> HandlerSymbols,
    ImmutableArray<InvocationExpressionSyntax> UseTinyCallsSyntax,
    AnalyzerConfigOptionsProvider Options)
{
    public static GeneratorInput Create(
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol?> handlers,
        ImmutableArray<InvocationExpressionSyntax?> useTinyCalls,
        AnalyzerConfigOptionsProvider options)
    {
        return new GeneratorInput(
            compilation,
            NormalizeHandlerSymbols(handlers),
            NormalizeUseTinyCalls(useTinyCalls),
            options);
    }

    private static ImmutableArray<INamedTypeSymbol> NormalizeHandlerSymbols(
        ImmutableArray<INamedTypeSymbol?> handlers)
    {
        if (handlers.IsDefaultOrEmpty)
        {
            return ImmutableArray<INamedTypeSymbol>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>(handlers.Length);

        for (var i = 0; i < handlers.Length; i++)
        {
            var handler = handlers[i];
            if (handler is not null)
            {
                builder.Add(handler);
            }
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<InvocationExpressionSyntax> NormalizeUseTinyCalls(
        ImmutableArray<InvocationExpressionSyntax?> useTinyCalls)
    {
        if (useTinyCalls.IsDefaultOrEmpty)
        {
            return ImmutableArray<InvocationExpressionSyntax>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<InvocationExpressionSyntax>(useTinyCalls.Length);

        for (var i = 0; i < useTinyCalls.Length; i++)
        {
            var useTinyCall = useTinyCalls[i];
            if (useTinyCall is not null)
            {
                builder.Add(useTinyCall);
            }
        }

        return builder.ToImmutable();
    }
}
