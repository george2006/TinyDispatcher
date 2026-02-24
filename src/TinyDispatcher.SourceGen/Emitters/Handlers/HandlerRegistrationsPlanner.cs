using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Emitters.Handlers;

internal static class HandlerRegistrationsPlanner
{
    public static HandlerRegistrationsPlan Build(DiscoveryResult result, GeneratorOptions options)
    {
        var ns = options.GeneratedNamespace;

        // Always emit a valid partial method.
        if (!options.EmitHandlerRegistrations)
            return HandlerRegistrationsPlan.Disabled(ns);

        // We need a context type to register ICommandHandler<TCommand,TContext>.
        // (Queries do not require it, but we keep a single consistent codepath.)
        var ctx = options.CommandContextType!;
        if (string.IsNullOrWhiteSpace(ctx))
            return HandlerRegistrationsPlan.Disabled(ns);

        var ctxFqn = Fqn.EnsureGlobal(ctx);

        var commands = CopyCommands(result.Commands);
        var queries = CopyQueries(result.Queries);

        Array.Sort(commands, CommandComparer.Instance);
        Array.Sort(queries, QueryComparer.Instance);

        return new HandlerRegistrationsPlan(
            @namespace: ns,
            isEnabled: true,
            commandContextFqn: ctxFqn,
            commands: commands,
            queries: queries);
    }

    private static HandlerContract[] CopyCommands(ImmutableArray<HandlerContract> source)
    {
        if (source.IsDefaultOrEmpty)
            return Array.Empty<HandlerContract>();

        var arr = new HandlerContract[source.Length];
        for (int i = 0; i < source.Length; i++)
            arr[i] = source[i];

        return arr;
    }

    private static QueryHandlerContract[] CopyQueries(ImmutableArray<QueryHandlerContract> source)
    {
        if (source.IsDefaultOrEmpty)
            return Array.Empty<QueryHandlerContract>();

        var arr = new QueryHandlerContract[source.Length];
        for (int i = 0; i < source.Length; i++)
            arr[i] = source[i];

        return arr;
    }

    private sealed class CommandComparer : IComparer<HandlerContract>
    {
        public static readonly CommandComparer Instance = new();

        public int Compare(HandlerContract x, HandlerContract y)
        {
            var c = string.Compare(x.MessageTypeFqn, y.MessageTypeFqn, StringComparison.Ordinal);
            if (c != 0) return c;

            return string.Compare(x.HandlerTypeFqn, y.HandlerTypeFqn, StringComparison.Ordinal);
        }
    }

    private sealed class QueryComparer : IComparer<QueryHandlerContract>
    {
        public static readonly QueryComparer Instance = new();

        public int Compare(QueryHandlerContract x, QueryHandlerContract y)
        {
            var c = string.Compare(x.QueryTypeFqn, y.QueryTypeFqn, StringComparison.Ordinal);
            if (c != 0) return c;

            c = string.Compare(x.ResultTypeFqn, y.ResultTypeFqn, StringComparison.Ordinal);
            if (c != 0) return c;

            return string.Compare(x.HandlerTypeFqn, y.HandlerTypeFqn, StringComparison.Ordinal);
        }
    }
}