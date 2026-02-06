using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using TinyDispatcher.SourceGen.Generator;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Discovery;

internal sealed class MiddlewareOrdering
{
    public ImmutableArray<MiddlewareRef> OrderAndDistinctGlobals(List<OrderedEntry> items)
    {
        var ordered = items
            .OrderBy(x => x.Order.FilePath, StringComparer.Ordinal)
            .ThenBy(x => x.Order.SpanStart);

        var seen = new HashSet<MiddlewareRef>();
        var list = new List<MiddlewareRef>();

        foreach (var x in ordered)
        {
            if (seen.Add(x.Middleware))
                list.Add(x.Middleware);
        }

        return list.ToImmutableArray();
    }

    public ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> BuildPerCommandMap(List<OrderedPerCommandEntry> items)
    {
        var ordered = items
            .OrderBy(x => x.Order.FilePath, StringComparer.Ordinal)
            .ThenBy(x => x.Order.SpanStart);

        var dict = new Dictionary<string, List<MiddlewareRef>>(StringComparer.Ordinal);

        foreach (var e in ordered)
        {
            if (!dict.TryGetValue(e.CommandFqn, out var list))
            {
                list = new List<MiddlewareRef>();
                dict[e.CommandFqn] = list;
            }

            list.Add(e.Middleware);
        }

        var builder = ImmutableDictionary.CreateBuilder<string, ImmutableArray<MiddlewareRef>>(StringComparer.Ordinal);

        foreach (var kv in dict)
        {
            var seen = new HashSet<MiddlewareRef>();
            var arr = kv.Value.Where(x => seen.Add(x)).ToImmutableArray();
            builder[kv.Key] = arr;
        }

        return builder.ToImmutable();
    }
}
