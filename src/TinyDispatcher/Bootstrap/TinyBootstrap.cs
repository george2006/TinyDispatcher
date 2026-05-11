using System;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher.Context;

namespace TinyDispatcher.Bootstrap;

public sealed class TinyBootstrap
{
    internal IServiceCollection Services { get; }
    internal TinyBootstrapContextFactorySelection ContextFactorySelection { get; private set; } =
        TinyBootstrapContextFactorySelection.None;

    public TinyBootstrap(IServiceCollection services)
        => Services = services ?? throw new ArgumentNullException(nameof(services));

    // Public fluent API users call
    public TinyBootstrap UseGlobalMiddleware(Type openGenericMiddlewareType)
    {
        DispatcherMiddlewareRegistrationExtensions.UseDispatcherCommandMiddleware(Services, openGenericMiddlewareType);
        return this;
    }

    public TinyBootstrap UseMiddlewareFor<TCommand>(Type openGenericMiddlewareType)
        where TCommand : ICommand
    {
        DispatcherMiddlewareRegistrationExtensions.UseDispatcherCommandMiddlewareFor<TCommand>(Services, openGenericMiddlewareType);
        return this;
    }

    public TinyBootstrap UseMiddlewareFor(Type commandType, Type openGenericMiddlewareType)
    {
        DispatcherMiddlewareRegistrationExtensions.UseDispatcherCommandMiddlewareFor(Services, commandType, openGenericMiddlewareType);
        return this;
    }

    public TinyBootstrap UsePolicy<TPolicy>() where TPolicy : class
    {
        PolicyRegistrationExtensions.UseTinyPolicy<TPolicy>(Services);
        return this;
    }

    public TinyBootstrap AddFeatureInitializer<TInitializer>()
        where TInitializer : class, IFeatureInitializer
    {
        Services.AddScoped<IFeatureInitializer, TInitializer>();
        return this;
    }

    public TinyBootstrap UseFactory<TFactory>()
        where TFactory : class
    {
        ContextFactorySelection = TinyBootstrapContextFactorySelection.FromFactoryType(typeof(TFactory));
        return this;
    }

    public TinyBootstrap UseDefaultFactory()
    {
        ContextFactorySelection = TinyBootstrapContextFactorySelection.DefaultFactory;
        return this;
    }
}
