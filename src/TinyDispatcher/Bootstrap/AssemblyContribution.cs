using System;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher.Bootstrap;

public class AssemblyContribution
{
    public AssemblyContribution(Action<IServiceCollection>? registerServices = null)
    {
        RegisterServices = registerServices;
    }

    public Action<IServiceCollection>? RegisterServices { get; }

    public virtual void Apply(IServiceCollection services)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        RegisterServices?.Invoke(services);
    }
}
