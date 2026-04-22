using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Generation.Emitters.Handlers;

internal sealed class HandlerRegistrationsPlan
{
    public HandlerRegistrationsPlan(
        string @namespace,
        bool isEnabled,
        HandlerContract[] commands,
        QueryHandlerContract[] queries)
    {
        Namespace = @namespace;
        IsEnabled = isEnabled;
        Commands = commands;
        Queries = queries;
    }

    public string Namespace { get; }

    public bool IsEnabled { get; }

    public HandlerContract[] Commands { get; }

    public QueryHandlerContract[] Queries { get; }

    public bool ShouldInsertBlankLineBetweenCommandAndQueryBlocks =>
        Commands.Length > 0 && Queries.Length > 0;

    public static HandlerRegistrationsPlan Disabled(string @namespace) =>
        new(
            @namespace: @namespace,
            isEnabled: false,
            commands: System.Array.Empty<HandlerContract>(),
            queries: System.Array.Empty<QueryHandlerContract>());
}
