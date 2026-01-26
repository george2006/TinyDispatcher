using System;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher;

public static class DispatcherMiddlewareRegistrationExtensions
{
    /// <summary>
    /// Registers a command middleware (GLOBAL) in the TinyDispatcher pipeline.
    /// Runtime no-op; source generator reads these invocations.
    /// Usage:
    /// services.UseDispatcherCommandMiddleware(typeof(LoggingMiddleware&lt;,&gt;));
    /// </summary>
    public static IServiceCollection UseDispatcherCommandMiddleware(
        this IServiceCollection services,
        Type openGenericMiddlewareType)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (openGenericMiddlewareType is null) throw new ArgumentNullException(nameof(openGenericMiddlewareType));
        if (!openGenericMiddlewareType.IsGenericTypeDefinition)
            throw new ArgumentException("Middleware must be an open generic type, e.g. typeof(MyMiddleware<,>)", nameof(openGenericMiddlewareType));

        return services; // runtime no-op; sourcegen hook
    }

    /// <summary>
    /// Registers a command middleware for a SPECIFIC command type.
    /// Runtime no-op; source generator reads these invocations.
    /// Usage:
    /// services.UseDispatcherCommandMiddlewareFor(typeof(CreateOrder), typeof(ValidationMiddleware&lt;,&gt;));
    /// </summary>
    public static IServiceCollection UseDispatcherCommandMiddlewareFor(
        this IServiceCollection services,
        Type commandType,
        Type openGenericMiddlewareType)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (commandType is null) throw new ArgumentNullException(nameof(commandType));
        if (openGenericMiddlewareType is null) throw new ArgumentNullException(nameof(openGenericMiddlewareType));
        if (!openGenericMiddlewareType.IsGenericTypeDefinition)
            throw new ArgumentException("Middleware must be an open generic type, e.g. typeof(MyMiddleware<,>)", nameof(openGenericMiddlewareType));

        return services; // runtime no-op; sourcegen hook
    }

    /// <summary>
    /// Typed sugar for per-command registration.
    /// </summary>
    public static IServiceCollection UseDispatcherCommandMiddlewareFor<TCommand>(
        this IServiceCollection services,
        Type openGenericMiddlewareType)
        where TCommand : ICommand
        => services.UseDispatcherCommandMiddlewareFor(typeof(TCommand), openGenericMiddlewareType);
}
