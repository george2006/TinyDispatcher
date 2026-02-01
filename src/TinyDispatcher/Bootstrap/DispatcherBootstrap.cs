using TinyDispatcher.Dispatching;
using TinyDispatcher.Internal;

namespace TinyDispatcher;

public static class DispatcherBootstrap
{
    private static readonly object _gate = new object();
    private static IDispatcherRegistry _cached = default!;
    public static void AddContribution(IMapContribution contribution)
        => ContributionStore.Add(contribution);

    public static IDispatcherRegistry BuildRegistry()
    {
        lock (_gate) 
        {

            if (_cached is not null) 
            {
                return _cached;
            }

            var (commands, queries) = ContributionStore.Drain();
            _cached = new DefaultDispatcherRegistry(commands, queries);
            return _cached;
        }
    }
}
