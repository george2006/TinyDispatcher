using System;
using System.Collections.Generic;
using System.Text;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal readonly struct OrderedEntry
{
    public readonly MiddlewareRef Middleware;
    public readonly OrderKey Order;

    public OrderedEntry(MiddlewareRef middleware, OrderKey order)
        => (Middleware, Order) = (middleware, order);
}

