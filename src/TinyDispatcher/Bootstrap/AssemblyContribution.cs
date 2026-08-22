using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher.Bootstrap;

public class AssemblyContribution
{
    private readonly Lazy<DispatcherOperationStructure[]>? _operations;

    public AssemblyContribution(Action<IServiceCollection>? registerServices = null)
    {
        RegisterServices = registerServices;
    }

    public AssemblyContribution(
        Action<IServiceCollection>? registerServices,
        Func<IReadOnlyCollection<DispatcherOperationStructure>> getOperations)
    {
        RegisterServices = registerServices;
        if (getOperations is null) throw new ArgumentNullException(nameof(getOperations));

        _operations = new Lazy<DispatcherOperationStructure[]>(() =>
        {
            var operations = getOperations()
                ?? throw new InvalidOperationException("The dispatcher operation factory returned null.");

            return operations.ToArray();
        });
    }

    public Action<IServiceCollection>? RegisterServices { get; }

    public virtual void Apply(IServiceCollection services)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        RegisterServices?.Invoke(services);
    }

    internal DispatcherOperationStructure[] GetOperationSnapshot()
    {
        return _operations?.Value.ToArray() ?? Array.Empty<DispatcherOperationStructure>();
    }
}
