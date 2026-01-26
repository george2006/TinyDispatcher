using System;
using System.Collections.Generic;

namespace TinyDispatcher;

public interface IMapContribution
{
    IEnumerable<KeyValuePair<Type, Type>> CommandHandlers { get; }
    IEnumerable<KeyValuePair<Type, Type>> QueryHandlers { get; }
}
