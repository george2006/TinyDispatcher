using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Extraction;

internal sealed class MiddlewareOrdering
{
    public ImmutableArray<MiddlewareRef> OrderAndDistinctGlobals(List<OrderedEntry> items)
    {
        var orderedEntries = CopyAndSort(items);
        var orderedMiddlewares = new List<MiddlewareRef>(orderedEntries.Count);

        for (var i = 0; i < orderedEntries.Count; i++)
        {
            orderedMiddlewares.Add(orderedEntries[i].Middleware);
        }

        return DistinctPreservingOrder(orderedMiddlewares);
    }

    public ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> BuildPerCommandMap(List<OrderedPerCommandEntry> items)
    {
        var orderedEntries = CopyAndSort(items);
        var middlewaresByCommand = GroupMiddlewaresByCommand(orderedEntries);

        return BuildImmutablePerCommandMap(middlewaresByCommand);
    }

    private static Dictionary<string, List<MiddlewareRef>> GroupMiddlewaresByCommand(
        List<OrderedPerCommandEntry> orderedEntries)
    {
        var middlewaresByCommand = new Dictionary<string, List<MiddlewareRef>>(StringComparer.Ordinal);

        for (var i = 0; i < orderedEntries.Count; i++)
        {
            var entry = orderedEntries[i];
            var commandMiddlewares = GetOrCreateList(middlewaresByCommand, entry.CommandFqn);

            commandMiddlewares.Add(entry.Middleware);
        }

        return middlewaresByCommand;
    }

    private static ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> BuildImmutablePerCommandMap(
        Dictionary<string, List<MiddlewareRef>> middlewaresByCommand)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, ImmutableArray<MiddlewareRef>>(StringComparer.Ordinal);

        foreach (var pair in middlewaresByCommand)
        {
            builder[pair.Key] = DistinctPreservingOrder(pair.Value);
        }

        return builder.ToImmutable();
    }

    private static List<MiddlewareRef> GetOrCreateList(
        Dictionary<string, List<MiddlewareRef>> middlewaresByCommand,
        string commandFqn)
    {
        if (middlewaresByCommand.TryGetValue(commandFqn, out var existing))
        {
            return existing;
        }

        var created = new List<MiddlewareRef>();
        middlewaresByCommand[commandFqn] = created;
        return created;
    }

    private static List<OrderedEntry> CopyAndSort(List<OrderedEntry> items)
    {
        var ordered = new List<OrderedEntry>(items);
        ordered.Sort(CompareEntries);
        return ordered;
    }

    private static List<OrderedPerCommandEntry> CopyAndSort(List<OrderedPerCommandEntry> items)
    {
        var ordered = new List<OrderedPerCommandEntry>(items);
        ordered.Sort(CompareEntries);
        return ordered;
    }

    private static int CompareEntries(OrderedEntry left, OrderedEntry right)
    {
        return CompareOrder(left.Order, right.Order);
    }

    private static int CompareEntries(OrderedPerCommandEntry left, OrderedPerCommandEntry right)
    {
        return CompareOrder(left.Order, right.Order);
    }

    private static int CompareOrder(OrderKey left, OrderKey right)
    {
        var filePathComparison = string.Compare(left.FilePath, right.FilePath, StringComparison.Ordinal);
        var filePathsAreDifferent = filePathComparison != 0;

        if (filePathsAreDifferent)
        {
            return filePathComparison;
        }

        return left.SpanStart.CompareTo(right.SpanStart);
    }

    private static ImmutableArray<MiddlewareRef> DistinctPreservingOrder(IEnumerable<MiddlewareRef> middlewares)
    {
        var seen = new HashSet<MiddlewareRef>();
        var builder = ImmutableArray.CreateBuilder<MiddlewareRef>();

        foreach (var middleware in middlewares)
        {
            var isFirstOccurrence = seen.Add(middleware);

            if (isFirstOccurrence)
            {
                builder.Add(middleware);
            }
        }

        return builder.ToImmutable();
    }
}
