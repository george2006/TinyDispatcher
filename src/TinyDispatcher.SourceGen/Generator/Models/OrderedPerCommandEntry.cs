using System;
using System.Collections.Generic;
using System.Text;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal readonly struct OrderedPerCommandEntry
{
    public readonly string CommandFqn;
    public readonly MiddlewareRef Middleware;
    public readonly OrderKey Order;

    public OrderedPerCommandEntry(string commandFqn, MiddlewareRef middleware, OrderKey order)
        => (CommandFqn, Middleware, Order) = (commandFqn, middleware, order);
}
