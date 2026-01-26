using System;
using System.Collections.Generic;

namespace TinyDispatcher.Dispatching;

public interface IDispatcherRegistry
{
    IReadOnlyDictionary<Type, Type> CommandHandlers { get; }
    IReadOnlyDictionary<Type, Type> QueryHandlers { get; }
}
