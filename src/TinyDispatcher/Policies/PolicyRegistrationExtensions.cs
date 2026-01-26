using System;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher;

public static class PolicyRegistrationExtensions
{
    /// <summary>
    /// Declares a policy type for source generation. Runtime no-op.
    /// </summary>
    public static IServiceCollection UseTinyPolicy(this IServiceCollection services, Type policyType)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (policyType is null) throw new ArgumentNullException(nameof(policyType));
        return services; // runtime no-op; sourcegen hook
    }

    /// <summary>
    /// Typed sugar for UseTinyPolicy(typeof(TPolicy)). Runtime no-op; sourcegen hook.
    /// </summary>
    public static IServiceCollection UseTinyPolicy<TPolicy>(this IServiceCollection services)
        where TPolicy : class
        => services.UseTinyPolicy(typeof(TPolicy));
}
