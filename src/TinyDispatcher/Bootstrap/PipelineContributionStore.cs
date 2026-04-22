using System;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher.Bootstrap;
internal static class PipelineContributionStore
{
    private static readonly object _gate = new();
    private static readonly List<IDispatcherAssemblyContribution> _items = new();

    public static void Add(IDispatcherAssemblyContribution contribution)
    {
        if (contribution is null) return;
        lock (_gate)
        {
            _items.Add(contribution);
        }
    }

    public static void Add(Action<IServiceCollection> contribution)
    {
        if (contribution is null) return;
        Add(new DelegateDispatcherAssemblyContribution(contribution));
    }

    public static IDispatcherAssemblyContribution[] Drain()
    {
        lock (_gate)
        {
            // Snapshot: do NOT clear. Allows multiple ServiceProviders in the same process.
            return _items.ToArray();
        }
    }

    internal static void ResetForTests()
    {
        lock (_gate)
        {
            _items.Clear();
        }
    }

    private sealed class DelegateDispatcherAssemblyContribution : IDispatcherAssemblyContribution
    {
        private readonly Action<IServiceCollection> _apply;

        public DelegateDispatcherAssemblyContribution(Action<IServiceCollection> apply)
        {
            _apply = apply;
        }

        public IReadOnlyList<CommandHandlerDescriptor> CommandHandlers { get; } =
            Array.Empty<CommandHandlerDescriptor>();

        public void Apply(IServiceCollection services)
        {
            _apply(services);
        }
    }
}
