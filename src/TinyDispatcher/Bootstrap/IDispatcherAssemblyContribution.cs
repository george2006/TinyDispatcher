using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher.Bootstrap;

public interface IDispatcherAssemblyContribution
{
    void Apply(IServiceCollection services);
}
