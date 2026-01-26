using System;
using System.Collections.Generic;
using System.Linq;
using TinyDispatcher.Dispatching;

namespace TinyDispatcher;

public sealed class DefaultDispatcherRegistry : IDispatcherRegistry
{
    public DefaultDispatcherRegistry(
        IEnumerable<KeyValuePair<Type, Type>> commandHandlers,
        IEnumerable<KeyValuePair<Type, Type>> queryHandlers)
    {
        if (commandHandlers is null) throw new ArgumentNullException(nameof(commandHandlers));
        if (queryHandlers is null) throw new ArgumentNullException(nameof(queryHandlers));

        CommandHandlers = commandHandlers.ToDictionary(kv => kv.Key, kv => kv.Value);
        QueryHandlers = queryHandlers.ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public IReadOnlyDictionary<Type, Type> CommandHandlers { get; }
    public IReadOnlyDictionary<Type, Type> QueryHandlers { get; }
}
