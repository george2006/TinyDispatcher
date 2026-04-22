using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher.Bootstrap;

public interface IDispatcherAssemblyContribution
{
    IReadOnlyList<CommandHandlerDescriptor> CommandHandlers { get; }

    void Apply(IServiceCollection services);
}
