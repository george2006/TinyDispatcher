using TinyDispatcher.Dispatching;
using TinyDispatcher.Internal;

namespace TinyDispatcher;

public static class DispatcherBootstrap
{
    public static void AddContribution(IMapContribution contribution)
        => ContributionStore.Add(contribution);

    public static IDispatcherRegistry BuildRegistry()
    {
        var (commands, queries) = ContributionStore.Drain();
        return new DefaultDispatcherRegistry(commands, queries);
    }
}
