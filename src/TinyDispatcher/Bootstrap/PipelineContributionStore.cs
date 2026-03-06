using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher.Bootstrap;
internal static class PipelineContributionStore
{
    private static readonly object _gate = new();
    private static readonly List<Action<IServiceCollection>> _items = new();

    public static void Add(Action<IServiceCollection> contribution)
    {
        if (contribution is null) return;
        lock (_gate)
        {
            _items.Add(contribution);
        }
    }

    public static Action<IServiceCollection>[] Drain()
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
}