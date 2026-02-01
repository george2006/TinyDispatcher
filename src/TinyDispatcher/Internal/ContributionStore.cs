using System;
using System.Collections.Generic;
using System.Linq;

namespace TinyDispatcher.Internal;

internal static class ContributionStore
{
    private static readonly object Gate = new();
    private static readonly List<IMapContribution> Items = new();

    public static void Add(IMapContribution contribution)
    {
        if (contribution is null) return;

        lock (Gate)
        {
            Items.Add(contribution);
        }
    }

    public static (IEnumerable<KeyValuePair<Type, Type>> Commands,
                   IEnumerable<KeyValuePair<Type, Type>> Queries) Drain()
    {
        lock (Gate)
        {
            var commands = Items.SelectMany(c => c.CommandHandlers).ToArray();
            var queries = Items.SelectMany(c => c.QueryHandlers).ToArray();
            return (commands, queries);
        }
    }
}
