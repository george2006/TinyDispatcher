#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;

namespace TinyDispatcher;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDispatcher<TContext>(
        this IServiceCollection services,
        Func<IServiceProvider, CancellationToken, ValueTask<TContext>>? contextFactory = null)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        // Registry is built from module-initializer contributions.
        services.AddSingleton<IDispatcherRegistry>(_ => DispatcherBootstrap.BuildRegistry());

        // Context factory rules (no guessing).
        if (contextFactory is not null)
        {
            services.Replace(
                ServiceDescriptor.Scoped(
                    typeof(IContextFactory<TContext>),
                    sp => new DelegateContextFactory<TContext>(sp, contextFactory)));
        }
        else
        {
            EnsureContextFactoryRegistered<TContext>(services);
        }

        services.AddScoped<IDispatcher<TContext>>(sp =>
            new Dispatcher<TContext>(
                sp,
                sp.GetRequiredService<IDispatcherRegistry>(),
                sp.GetRequiredService<IContextFactory<TContext>>()));

        return services;
    }

    public static IServiceCollection UseTinyDispatcher<TContext>(
        this IServiceCollection services,
        Action<TinyBootstrap> configure,
        Func<IServiceProvider, CancellationToken, ValueTask<TContext>>? contextFactory = null)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        // User config (middleware, policies, etc.)
        configure?.Invoke(new TinyBootstrap(services));

        if (contextFactory is null && typeof(TContext) == typeof(AppContext))
        {
            services.TryAddScoped<IContextFactory<TContext>>(sp =>
                (IContextFactory<TContext>)(object)
                new DefaultAppContextFactory(sp.GetServices<IFeatureInitializer>()));
        }

        // Core registration
        services.AddDispatcher<TContext>(contextFactory);

        // Apply generated pipeline registrations contributed by module initializers
        DispatcherPipelineBootstrap.Apply(services);

        return services;
    }

    private static void EnsureContextFactoryRegistered<TContext>(IServiceCollection services)
    {
        var serviceType = typeof(IContextFactory<TContext>);
        var found = services.Any(d => d.ServiceType == serviceType);
        if (!found)
        {
            throw new InvalidOperationException(
                "TinyDispatcher: no context factory is registered for the requested context type. " +
                "Either register IContextFactory<TContext> before calling AddDispatcher, " +
                "or pass a contextFactory callback to AddDispatcher<TContext>(..., contextFactory: ...).");
        }
    }

    private sealed class DelegateContextFactory<TContext> : IContextFactory<TContext>
    {
        private readonly IServiceProvider _sp;
        private readonly Func<IServiceProvider, CancellationToken, ValueTask<TContext>> _factory;

        public DelegateContextFactory(
            IServiceProvider sp,
            Func<IServiceProvider, CancellationToken, ValueTask<TContext>> factory)
            => (_sp, _factory) = (sp, factory);

        public ValueTask<TContext> CreateAsync(CancellationToken ct = default)
            => _factory(_sp, ct);
    }
}
