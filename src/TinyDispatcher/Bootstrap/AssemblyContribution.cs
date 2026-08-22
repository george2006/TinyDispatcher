using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher.Bootstrap;

public class AssemblyContribution
{
    private readonly DispatcherOperation[] _operations;

    public AssemblyContribution(
        Action<IServiceCollection>? registerServices = null,
        IReadOnlyCollection<DispatcherOperation>? operations = null)
    {
        RegisterServices = registerServices;
        _operations = operations?.ToArray() ?? Array.Empty<DispatcherOperation>();
    }

    public Action<IServiceCollection>? RegisterServices { get; }

    public virtual void Apply(IServiceCollection services)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        RegisterServices?.Invoke(services);
    }

    internal DispatcherOperation[] GetOperationSnapshot()
    {
        return _operations.ToArray();
    }
}
