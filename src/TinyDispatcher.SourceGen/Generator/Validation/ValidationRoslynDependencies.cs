#nullable enable

using Microsoft.CodeAnalysis;
using System;

namespace TinyDispatcher.SourceGen.Generator.Validation;

internal sealed class ValidationRoslynDependencies
{
    public ValidationRoslynDependencies(
        INamedTypeSymbol? commandMiddlewareInterface,
        MiddlewareTypeResolver middlewareTypeResolver)
    {
        CommandMiddlewareInterface = commandMiddlewareInterface;
        MiddlewareTypeResolver = middlewareTypeResolver ?? throw new ArgumentNullException(nameof(middlewareTypeResolver));
    }

    public INamedTypeSymbol? CommandMiddlewareInterface { get; }
    public MiddlewareTypeResolver MiddlewareTypeResolver { get; }

    public static ValidationRoslynDependencies Create(Compilation compilation)
    {
        if (compilation is null)
            throw new ArgumentNullException(nameof(compilation));

        return new ValidationRoslynDependencies(
            compilation.GetTypeByMetadataName("TinyDispatcher.ICommandMiddleware`2"),
            new MiddlewareTypeResolver(compilation));
    }
}
